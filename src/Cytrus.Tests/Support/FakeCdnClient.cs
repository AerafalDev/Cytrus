using Cytrus.Cdn;
using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;

namespace Cytrus.Tests.Support;

public sealed class FakeCdnClient(byte[] manifest, IReadOnlyDictionary<string, byte[]> bundles) : ICytrusCdnClient
{
    private int _rangeRequests;

    public int RangeRequests =>
        _rangeRequests;

    public Task<CytrusIndex> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Explicit version supplied; the index should not be fetched.");
    }

    public Task<byte[]> GetManifestAsync(GameCoordinates coordinates, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(manifest);
    }

    public async Task<DownloadedRange> DownloadRangeAsync(
        string game,
        HashId bundleHash,
        ByteRange range,
        Func<DownloadedRange, Stream, CancellationToken, Task> onContent,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _rangeRequests);

        var data = bundles[bundleHash.Hex];
        var slice = data.AsSpan((int)range.Start, (int)range.Length).ToArray();

        var downloaded = new DownloadedRange(range.Start, slice.Length, WholeBundleReturned: false);

        using var ms = new MemoryStream(slice);
        await onContent(downloaded, ms, cancellationToken);

        return downloaded;
    }
}
