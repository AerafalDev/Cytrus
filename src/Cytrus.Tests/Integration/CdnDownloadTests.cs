using System.Security.Cryptography;
using Cytrus.Download;
using Cytrus.Hash;
using Cytrus.Models;

namespace Cytrus.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("cdn-integration")]
public class CdnDownloadTests(CdnIntegrationFixture fixture)
{
    [Fact]
    public void ManifestHasContent()
    {
        Assert.False(string.IsNullOrWhiteSpace(fixture.Version));
        Assert.NotEmpty(fixture.Manifest.Fragments);
        Assert.True(fixture.Manifest.TotalFileCount > 0);
    }

    [Fact]
    public async Task DownloadsSmallestFileAndBytesMatchManifestHash()
    {
        var file = fixture.SmallestNonEmptyFile();
        await DownloadAndVerifyAsync(file);
    }

    [Fact]
    public async Task DownloadsSingleChunkFileAndVerifies()
    {
        var file = fixture.SmallestSingleChunkFile();
        Assert.Empty(file.Chunks);
        await DownloadAndVerifyAsync(file);
    }

    [Fact]
    public async Task DownloadsMultiBundleFileAndVerifies()
    {
        var file = fixture.SmallestMultiBundleFile();
        Assert.NotNull(file);
        await DownloadAndVerifyAsync(file);
    }

    [Fact]
    public async Task ReRunningSkipsUpToDateFile()
    {
        var file = fixture.SmallestNonEmptyFile();
        using var dir = new TempDir();

        var first = await Download(file, dir.Path);
        Assert.Equal(1, first.FilesWritten);

        var second = await Download(file, dir.Path);
        Assert.Equal(0, second.FilesWritten);
        Assert.Equal(1, second.FilesSkipped);
    }

    private async Task DownloadAndVerifyAsync(FileEntry file)
    {
        using var dir = new TempDir();
        var result = await Download(file, dir.Path);

        Assert.Equal(1, result.FilesWritten);

        var path = Path.Combine(dir.Path, file.Name.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Expected file '{file.Name}' on disk.");
        Assert.Equal(file.Size, new FileInfo(path).Length);

        await using var stream = File.OpenRead(path);
        var actual = new HashId(await SHA1.HashDataAsync(stream));
        Assert.Equal(file.Hash, actual);
    }

    private Task<DownloadResult> Download(FileEntry file, string output)
    {
        return fixture.Downloader.DownloadAsync(new DownloadRequest { Coordinates = fixture.BaseCoordinates.WithVersion(fixture.Version), OutputDirectory = output, Select = [file.Name], });
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cytrus-it-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // ignore
            }
        }
    }
}
