using System.Security.Cryptography;
using Cytrus.Assembly;
using Cytrus.Exceptions;
using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;
using Cytrus.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cytrus.Tests.Unit;

public sealed class FileAssemblerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cytrus-assembler-" + Guid.NewGuid().ToString("N"));
    private readonly FileAssembler _assembler = new(NullLogger<FileAssembler>.Instance);

    private static HashId Sha1(ReadOnlySpan<byte> data)
    {
        return new HashId(SHA1.HashData(data));
    }

    [Fact]
    public async Task ReassemblesMultiChunkFileAndVerifies()
    {
        var a = new byte[] { 1, 2, 3, 4, 5 };
        var b = new byte[] { 9, 8, 7 };
        var bundleBytes = a.Concat(b).ToArray();
        var bundleHash = HashId.Parse("3f01");

        var store = new InMemoryBundleStore();
        store.AddBundle(bundleHash, bundleBytes);

        var placements = new[]
        {
            new ChunkPlacement(bundleHash, 0, a.Length, Sha1(a)),
            new ChunkPlacement(bundleHash, a.Length, b.Length, Sha1(b)),
        };

        var file = new FileEntry("dir/out.bin", bundleBytes.Length, Sha1(bundleBytes), [], false, null);
        var plan = new FilePlan(file, placements);

        var result = await _assembler.AssembleAsync(plan, store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.Written, result.Status);
        var written = await File.ReadAllBytesAsync(Path.Combine(_root, "dir", "out.bin"));
        Assert.Equal(bundleBytes, written);
    }

    [Fact]
    public async Task SingleChunkFileKeyedByFileHashIsWritten()
    {
        var content = "*+,-"u8.ToArray();
        var bundleHash = HashId.Parse("ab12");
        var store = new InMemoryBundleStore();
        store.AddBundle(bundleHash, content);

        var fileHash = Sha1(content);
        var placements = new[] { new ChunkPlacement(bundleHash, 0, content.Length, fileHash) };
        var file = new FileEntry("single.bin", content.Length, fileHash, [], false, null);

        var result = await _assembler.AssembleAsync(new FilePlan(file, placements), store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.Written, result.Status);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(_root, "single.bin")));
    }

    [Fact]
    public async Task CorruptedChunkIsDetectedAndNoFileIsLeftBehind()
    {
        var expected = new byte[] { 1, 2, 3, 4 };
        var corrupted = new byte[] { 1, 2, 3, 99 };
        var bundleHash = HashId.Parse("dead");

        var store = new InMemoryBundleStore();
        store.AddBundle(bundleHash, corrupted);

        var placements = new[] { new ChunkPlacement(bundleHash, 0, expected.Length, Sha1(expected)) };
        var file = new FileEntry("bad.bin", expected.Length, Sha1(expected), [], false, null);

        await Assert.ThrowsAsync<IntegrityException>(() => _assembler.AssembleAsync(new FilePlan(file, placements), store, _root, AssemblyOptions.Default));

        Assert.False(File.Exists(Path.Combine(_root, "bad.bin")));
        Assert.False(File.Exists(Path.Combine(_root, "bad.bin.cytmp")));
    }

    [Fact]
    public async Task FileLevelHashMismatchIsDetectedWhenChunkVerificationDisabled()
    {
        var corrupted = new byte[] { 5, 6, 7, 8 };
        var bundleHash = HashId.Parse("beef");
        var store = new InMemoryBundleStore();
        store.AddBundle(bundleHash, corrupted);

        var placements = new[] { new ChunkPlacement(bundleHash, 0, corrupted.Length, Sha1(corrupted)) };
        var file = new FileEntry("mismatch.bin", corrupted.Length, Sha1([0, 0, 0, 0]), [], false, null);
        var options = AssemblyOptions.Default with { VerifyChunks = false };

        await Assert.ThrowsAsync<IntegrityException>(() => _assembler.AssembleAsync(new FilePlan(file, placements), store, _root, options));
    }

    [Fact]
    public async Task UpToDateFileIsSkippedOnSecondRun()
    {
        var content = new byte[] { 10, 20, 30 };
        var bundleHash = HashId.Parse("0a0b");
        var store = new InMemoryBundleStore();
        store.AddBundle(bundleHash, content);

        var placements = new[] { new ChunkPlacement(bundleHash, 0, content.Length, Sha1(content)) };
        var file = new FileEntry("dup.bin", content.Length, Sha1(content), [], false, null);
        var plan = new FilePlan(file, placements);

        var first = await _assembler.AssembleAsync(plan, store, _root, AssemblyOptions.Default);
        var second = await _assembler.AssembleAsync(plan, store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.Written, first.Status);
        Assert.Equal(FileAssemblyStatus.Skipped, second.Status);
    }

    [Fact]
    public async Task ZeroByteFileIsCreatedEmpty()
    {
        var store = new InMemoryBundleStore();
        var file = new FileEntry("empty.dat", 0, default, [], false, null);
        var result = await _assembler.AssembleAsync(new FilePlan(file, []), store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.Written, result.Status);
        Assert.True(File.Exists(Path.Combine(_root, "empty.dat")));
        Assert.Empty(await File.ReadAllBytesAsync(Path.Combine(_root, "empty.dat")));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // ignore
        }
    }
}
