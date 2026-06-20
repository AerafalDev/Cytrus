using System.ComponentModel;
using System.Diagnostics;
using Cytrus.Assembly;
using Cytrus.Cli.Download;
using Cytrus.Download;
using Cytrus.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cytrus.Cli.Commands;

public sealed class DownloadCommand(IGameDownloader downloader) : AsyncCommand<DownloadCommand.Settings>
{
    public sealed class Settings : TargetSettings
    {
        [CommandOption("-o|--output <DIR>")]
        [Description("Output directory.")]
        [DefaultValue("./output")]
        public string Output { get; init; } = "./output";

        [CommandOption("-v|--version <VERSION>")]
        [Description("Specific version to download (defaults to the latest for the channel).")]
        public string? Version { get; init; }

        [CommandOption("-s|--select <PATTERN>")]
        [Description("Glob pattern(s) selecting files to download (repeatable). Omit to download everything.")]
        public string[] Select { get; init; } = [];

        [CommandOption("--concurrency <N>")]
        [Description("Max concurrent bundle downloads.")]
        public int? Concurrency { get; init; }

        [CommandOption("--no-verify")]
        [Description("Disable SHA-1 verification of chunks and files (faster, unsafe).")]
        [DefaultValue(false)]
        public bool NoVerify { get; init; }

        [CommandOption("--force")]
        [Description("Re-download files even if an up-to-date copy already exists.")]
        [DefaultValue(false)]
        public bool Force { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        GameCoordinates coordinates;

        try
        {
            coordinates = new GameCoordinates(settings.Game, settings.Platform, settings.Release, settings.Version);
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Invalid argument:[/] {ex.Message}");
            return 2;
        }

        var assembly = new AssemblyOptions
        {
            VerifyChunks = !settings.NoVerify,
            VerifyFiles = !settings.NoVerify,
            SkipUpToDate = !settings.Force
        };

        var output = Path.GetFullPath(settings.Output);

        AnsiConsole.MarkupLineInterpolated($"Downloading [yellow]{coordinates.Game}[/]/[yellow]{coordinates.Release}[/] ([grey]{coordinates.Platform}[/]) into [blue]{output}[/]");

        var stopwatch = Stopwatch.StartNew();
        DownloadResult result = null!;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new DownloadedColumn(),
                new TransferSpeedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var sink = new SpectreProgressSink(ctx);

                var request = new DownloadRequest
                {
                    Coordinates = coordinates,
                    OutputDirectory = output,
                    Select = settings.Select.Length > 0 ? settings.Select : null,
                    Assembly = assembly,
                    MaxParallelDownloads = settings.Concurrency,
                    Progress = sink
                };

                result = await downloader.DownloadAsync(request, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

        stopwatch.Stop();
        AnsiConsole.MarkupLineInterpolated($"[green]Done[/] version [yellow]{result.Version}[/] in {stopwatch.Elapsed.TotalSeconds:F1}s — {result.FilesWritten} written, {result.FilesSkipped} skipped, {result.Symlinks} symlinks, {Bytes(result.BytesDownloaded)} downloaded.");
        return 0;
    }

    private static string Bytes(long value)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = value;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
