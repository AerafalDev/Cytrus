using Cytrus.Assembly;
using Cytrus.Cdn;
using Cytrus.Download;
using Cytrus.Manifest;
using Cytrus.Models;
using Cytrus.Planning;
using Cytrus.Selection;
using Cytrus.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cytrus.Tests.Unit;

public sealed class GameDownloaderTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "cytrus-dl-" + Guid.NewGuid().ToString("N"));

    private static byte[] Bytes(int n, int seed)
    {
        return [.. Enumerable.Range(0, n).Select(i => (byte)((i + seed) % 256))];
    }

    private static GameDownloader NewDownloader(FakeCdnClient cdn)
    {
        return new GameDownloader(
            new GameVersionResolver(cdn),
            cdn,
            new FlatSharpManifestReader(),
            new DownloadPlanner(),
            new GlobFileSelectorFactory(),
            new FileAssembler(NullLogger<FileAssembler>.Instance),
            NullLogger<GameDownloader>.Instance);
    }

    private DownloadRequest Request(string[]? select = null)
    {
        return new DownloadRequest { Coordinates = new GameCoordinates("dofus", "windows", "dofus3", FakeGameBuilder.Version), OutputDirectory = _out, Select = select };
    }

    private static (FakeCdnClient cdn, IReadOnlyDictionary<string, byte[]> expected) BuildGame()
    {
        var single = Bytes(50, 0);
        var m1 = Bytes(20, 100);
        var m2 = Bytes(10, 50);
        var c1 = Bytes(40, 200);
        var c2 = Bytes(25, 7);

        var b = new FakeGameBuilder();
        var a = b.AddBundle("A", ("single", single), ("m1", m1), ("m2", m2), ("c1", c1));
        var bb = b.AddBundle("B", ("c2", c2));

        b.AddSingleChunkFile("data/single.bin", single);
        b.AddFile("data/multi.bin", [.. m1, .. m2], a["m1"].hash, a["m2"].hash);
        b.AddFile("cross/cross.bin", [.. c1, .. c2], a["c1"].hash, bb["c2"].hash);
        b.AddEmptyFile("empty.dat");
        b.AddSymlink("link.bin", "data/single.bin");

        return b.Build();
    }

    [Fact]
    public async Task DownloadsAndVerifiesAllFileShapesOffline()
    {
        var (cdn, expected) = BuildGame();
        var result = await NewDownloader(cdn).DownloadAsync(Request());

        Assert.Equal(4, result.FilesWritten);

        foreach (var (name, content) in expected)
        {
            var path = Path.Combine(_out, name.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"missing {name}");
            Assert.Equal(content, await File.ReadAllBytesAsync(path));
        }
    }

    [Fact]
    public async Task CrossBundleFilePullsFromBothBundles()
    {
        var (cdn, _) = BuildGame();
        await NewDownloader(cdn).DownloadAsync(Request(["**/cross.bin"]));

        Assert.True(cdn.RangeRequests >= 2);
        Assert.Equal(Bytes(40, 200).Concat(Bytes(25, 7)), await File.ReadAllBytesAsync(Path.Combine(_out, "cross", "cross.bin")));
    }

    [Fact]
    public async Task SelectionLimitsToMatchingFilesAndBundles()
    {
        var (cdn, _) = BuildGame();
        var result = await NewDownloader(cdn).DownloadAsync(Request(["**/multi.bin"]));

        Assert.Equal(1, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(_out, "data", "multi.bin")));
        Assert.False(File.Exists(Path.Combine(_out, "data", "single.bin")));
        Assert.Equal(1, cdn.RangeRequests);
    }

    [Fact]
    public async Task EmptySelectionMatchWritesNothing()
    {
        var (cdn, _) = BuildGame();
        var result = await NewDownloader(cdn).DownloadAsync(Request(["**/*.nomatch"]));

        Assert.Equal(0, result.FilesWritten);
        Assert.Equal(0, cdn.RangeRequests);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_out))
                Directory.Delete(_out, recursive: true);
        }
        catch (IOException)
        {
            // ignore
        }
    }
}
