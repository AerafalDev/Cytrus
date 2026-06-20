using System.Collections.Concurrent;
using Cytrus.Hash;
using Microsoft.Win32.SafeHandles;

namespace Cytrus.Storage;

public sealed class TempFileBundleStore : IBundleStore
{
    private readonly string _directory;
    private readonly ConcurrentDictionary<string, SafeFileHandle> _readHandles;

    public TempFileBundleStore(string directory)
    {
        _directory = directory;
        _readHandles = [];
        Directory.CreateDirectory(_directory);
    }

    public static TempFileBundleStore CreateUnique(string baseTempDirectory, string scope)
    {
        return new TempFileBundleStore(Path.Combine(baseTempDirectory, "cytrus", scope, Guid.NewGuid().ToString("N")));
    }

    private string PathFor(HashId bundleHash)
    {
        return Path.Combine(_directory, bundleHash.Hex);
    }

    public Stream OpenWrite(HashId bundleHash)
    {
        return new FileStream(PathFor(bundleHash), new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous
        });
    }

    public async ValueTask ReadExactAsync(HashId bundleHash, long offset, Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        var handle = _readHandles.GetOrAdd(bundleHash.Hex, _ => File.OpenHandle(PathFor(bundleHash), FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess));
        var totalRead = 0;

        while (totalRead < destination.Length)
        {
            var read = await RandomAccess.ReadAsync(handle, destination[totalRead..], offset + totalRead, cancellationToken).ConfigureAwait(false);

            if (read is 0)
                throw new EndOfStreamException($"Bundle {bundleHash.Hex} ended early while reading {destination.Length} bytes at offset {offset}.");

            totalRead += read;
        }
    }

    public void Clear()
    {
        foreach (var handle in _readHandles.Values)
            handle.Dispose();

        _readHandles.Clear();

        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // ignore
        }
    }

    public ValueTask DisposeAsync()
    {
        Clear();
        return ValueTask.CompletedTask;
    }
}
