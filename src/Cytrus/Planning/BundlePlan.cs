using Cytrus.Hash;

namespace Cytrus.Planning;

public sealed record BundlePlan(HashId BundleHash, IReadOnlyList<ByteRange> Ranges)
{
    public long DownloadBytes =>
        Ranges.Sum(static x => x.Length);
}
