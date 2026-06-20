using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;

namespace Cytrus.Cdn;

public interface ICytrusCdnClient
{
    Task<CytrusIndex> GetIndexAsync(CancellationToken cancellationToken = default);

    Task<byte[]> GetManifestAsync(GameCoordinates coordinates, CancellationToken cancellationToken = default);

    Task<DownloadedRange> DownloadRangeAsync(
        string game,
        HashId bundleHash,
        ByteRange range,
        Func<DownloadedRange, Stream, CancellationToken, Task> onContent,
        CancellationToken cancellationToken = default);
}
