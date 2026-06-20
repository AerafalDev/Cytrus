using Cytrus.Hash;
using Cytrus.Storage;

namespace Cytrus.Tests.Support;

public sealed class InMemoryBundleStore : IBundleStore
{
    private readonly Dictionary<string, byte[]> _bundles = new();

    public void AddBundle(HashId hash, byte[] data)
    {
        _bundles[hash.Hex] = data;
    }

    public Stream OpenWrite(HashId bundleHash)
    {
        throw new NotSupportedException();
    }

    public ValueTask ReadExactAsync(HashId bundleHash, long offset, Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        var data = _bundles[bundleHash.Hex];
        data.AsSpan((int)offset, destination.Length).CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }

    public void Clear()
    {
        _bundles.Clear();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
