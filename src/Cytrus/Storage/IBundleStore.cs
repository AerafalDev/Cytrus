using Cytrus.Hash;

namespace Cytrus.Storage;

public interface IBundleStore : IAsyncDisposable
{
    Stream OpenWrite(HashId bundleHash);

    ValueTask ReadExactAsync(HashId bundleHash, long offset, Memory<byte> destination, CancellationToken cancellationToken = default);

    void Clear();
}
