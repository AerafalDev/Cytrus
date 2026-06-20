using System.Text.RegularExpressions;
using Cytrus.Cdn;
using Cytrus.Download;
using Cytrus.Extensions;
using Cytrus.Hash;
using Cytrus.Manifest;
using Cytrus.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cytrus.Tests.Integration;

public sealed partial class CdnIntegrationFixture : IAsyncLifetime
{
    public ServiceProvider Provider { get; private set; } = null!;

    public GameManifest Manifest { get; private set; } = null!;

    public string Version { get; private set; } = null!;

    public GameCoordinates BaseCoordinates { get; } = new("dofus", "windows", "dofus3");

    public ICytrusCdnClient Cdn =>
        Provider.GetRequiredService<ICytrusCdnClient>();
    public IGameDownloader Downloader =>
        Provider.GetRequiredService<IGameDownloader>();

    public IEnumerable<FileEntry> AllFiles =>
        Manifest.Fragments.SelectMany(static f => f.Files).Where(static f => SafeName().IsMatch(f.Name) && !f.IsSymlink);

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCytrus();
        Provider = services.BuildServiceProvider();

        var resolver = Provider.GetRequiredService<IGameVersionResolver>();
        Version = await resolver.ResolveVersionAsync(BaseCoordinates);

        var reader = Provider.GetRequiredService<IManifestReader>();
        var bytes = await Cdn.GetManifestAsync(BaseCoordinates.WithVersion(Version));
        Manifest = reader.Read(bytes);
    }

    public Task DisposeAsync()
    {
        Provider.Dispose();
        return Task.CompletedTask;
    }

    public FileEntry SmallestNonEmptyFile()
    {
        return AllFiles.Where(static f => f.Size > 0).OrderBy(static f => f.Size).First();
    }

    public FileEntry SmallestSingleChunkFile()
    {
        return AllFiles.Where(static f => f is { Size: > 0, Chunks.Count: 0 }).OrderBy(static f => f.Size).First();
    }

    public FileEntry? SmallestMultiBundleFile()
    {
        foreach (var fragment in Manifest.Fragments)
        {
            var chunkToBundle = new Dictionary<HashId, HashId>();

            foreach (var bundle in fragment.Bundles)
                foreach (var chunk in bundle.Chunks)
                    chunkToBundle.TryAdd(chunk.Hash, bundle.Hash);

            FileEntry? best = null;

            foreach (var file in fragment.Files)
            {
                if (file.Chunks.Count < 2 || file.IsSymlink || !SafeName().IsMatch(file.Name))
                    continue;

                var bundles = new HashSet<HashId>();
                foreach (var chunk in file.Chunks)
                    if (chunkToBundle.TryGetValue(chunk.Hash, out var bundleHash))
                        bundles.Add(bundleHash);

                if (bundles.Count >= 2 && (best is null || file.Size < best.Size))
                    best = file;
            }

            if (best is not null)
                return best;
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_./ -]+$", RegexOptions.Compiled)]
    private static partial Regex SafeName();
}
