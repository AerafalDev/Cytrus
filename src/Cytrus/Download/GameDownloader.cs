using System.Buffers;
using Cytrus.Assembly;
using Cytrus.Cdn;
using Cytrus.Manifest;
using Cytrus.Models;
using Cytrus.Planning;
using Cytrus.Selection;
using Cytrus.Storage;
using Microsoft.Extensions.Logging;

namespace Cytrus.Download;

public sealed partial class GameDownloader(
    IGameVersionResolver versionResolver,
    ICytrusCdnClient cdnClient,
    IManifestReader manifestReader,
    IDownloadPlanner planner,
    IFileSelectorFactory selectorFactory,
    IFileAssembler assembler,
    ILogger<GameDownloader> logger) : IGameDownloader
{
    private const int CopyBufferSize = 1 << 20;

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var progress = request.Progress ?? NullDownloadProgressSink.Instance;

        var version = await versionResolver.ResolveVersionAsync(request.Coordinates, cancellationToken).ConfigureAwait(false);
        var coordinates = request.Coordinates.WithVersion(version);

        LogResolvedCoordinates(coordinates);

        var manifestBytes = await cdnClient.GetManifestAsync(coordinates, cancellationToken).ConfigureAwait(false);
        var manifest = manifestReader.Read(manifestBytes);

        var selector = selectorFactory.Create(request.Select);
        var outputRoot = Path.GetFullPath(request.OutputDirectory);
        Directory.CreateDirectory(outputRoot);

        var plans = new List<FragmentPlan>();

        foreach (var fragment in manifest.Fragments)
        {
            var plan = planner.Plan(fragment, selector, request.Planner);

            if (!plan.IsEmpty)
                plans.Add(plan);
        }

        var totalDownloadBytes = plans.Sum(static p => p.TotalDownloadBytes);
        var totalFiles = plans.Sum(static p => p.Files.Count);

        progress.ReportPlan(totalDownloadBytes, totalFiles, plans.Count);

        if (totalFiles is 0)
        {
            LogNothingMatchedTheSelectionNoFilesToDownload();
            return new DownloadResult(version, 0, 0, 0, 0);
        }

        var maxParallel = NormalizeParallelism(request.MaxParallelDownloads);
        var counters = new Counters();

        for (var i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];

            progress.BeginFragment(plan.FragmentName, i + 1, plans.Count, plan.TotalDownloadBytes, plan.Files.Count);

            await using var store = TempFileBundleStore.CreateUnique(Path.GetTempPath(), coordinates.Game);

            await DownloadBundlesAsync(coordinates.Game, plan, store, progress, counters, maxParallel, cancellationToken).ConfigureAwait(false);

            await AssembleFilesAsync(plan, store, outputRoot, request.Assembly, progress, counters, maxParallel, cancellationToken).ConfigureAwait(false);

            progress.EndFragment(plan.FragmentName);
        }

        return new DownloadResult(
            version,
            counters.FilesWritten,
            counters.FilesSkipped,
            counters.Symlinks,
            Interlocked.Read(ref counters.BytesDownloaded));
    }

    private async Task DownloadBundlesAsync(
        string game,
        FragmentPlan plan,
        TempFileBundleStore store,
        IDownloadProgressSink progress,
        Counters counters,
        int maxParallel,
        CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(plan.Bundles, parallelOptions, async (bundle, ct) =>
        {
            var writeStream = store.OpenWrite(bundle.BundleHash);

            try
            {
                foreach (var range in bundle.Ranges)
                {
                    await cdnClient.DownloadRangeAsync(game, bundle.BundleHash, range, async (downloaded, content, innerCt) =>
                    {
                        writeStream.Seek(downloaded.Start, SeekOrigin.Begin);
                        await CopyCountingAsync(content, writeStream, progress, counters, innerCt).ConfigureAwait(false);
                    }, ct).ConfigureAwait(false);
                }

                await writeStream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                await writeStream.DisposeAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    private async Task AssembleFilesAsync(
        FragmentPlan plan,
        IBundleStore store,
        string outputRoot,
        AssemblyOptions assemblyOptions,
        IDownloadProgressSink progress,
        Counters counters,
        int maxParallel,
        CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(plan.Files, parallelOptions, async (filePlan, ct) =>
        {
            var result = await assembler.AssembleAsync(filePlan, store, outputRoot, assemblyOptions, ct).ConfigureAwait(false);

            counters.Record(result.Status);
            progress.ReportFileCompleted(result);
        }).ConfigureAwait(false);
    }

    private static async Task CopyCountingAsync(
        Stream source,
        Stream destination,
        IDownloadProgressSink progress,
        Counters counters,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);

        try
        {
            int read;

            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                Interlocked.Add(ref counters.BytesDownloaded, read);
                progress.ReportDownloadedBytes(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int NormalizeParallelism(int? requested)
    {
        if (requested is { } value)
            return Math.Clamp(value, 1, 64);

        return Math.Clamp(Environment.ProcessorCount * 2, 4, 16);
    }

    private sealed class Counters
    {
        public long BytesDownloaded;
        private int _filesWritten;
        private int _filesSkipped;
        private int _symlinks;

        public int FilesWritten =>
            _filesWritten;

        public int FilesSkipped =>
            _filesSkipped;

        public int Symlinks =>
            _symlinks;

        public void Record(FileAssemblyStatus status)
        {
            switch (status)
            {
                case FileAssemblyStatus.Written:
                    Interlocked.Increment(ref _filesWritten);
                    break;
                case FileAssemblyStatus.Skipped:
                    Interlocked.Increment(ref _filesSkipped);
                    break;
                case FileAssemblyStatus.SymlinkCreated:
                    Interlocked.Increment(ref _symlinks);
                    break;
                case FileAssemblyStatus.SymlinkUnsupported:
                    break;
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "Nothing matched the selection; no files to download")]
    partial void LogNothingMatchedTheSelectionNoFilesToDownload();

    [LoggerMessage(LogLevel.Information, "Resolved {Coordinates}")]
    partial void LogResolvedCoordinates(GameCoordinates coordinates);
}
