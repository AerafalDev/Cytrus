using System.Security;
using Cytrus.Assembly;
using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;
using Cytrus.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cytrus.Tests.Unit;

public sealed class FileAssemblerExtraTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cytrus-asm2-" + Guid.NewGuid().ToString("N"));
    private readonly FileAssembler _assembler = new(NullLogger<FileAssembler>.Instance);

    [Fact]
    public async Task PathTraversalInFileNameIsBlocked()
    {
        var store = new InMemoryBundleStore();
        var file = new FileEntry("../escape.bin", 4, Hashes.Sha1([1, 2, 3, 4]), [], false, null);
        var plan = new FilePlan(file, [new ChunkPlacement(HashId.Parse("aa"), 0, 4, Hashes.Sha1([1, 2, 3, 4]))]);

        await Assert.ThrowsAsync<SecurityException>(() => _assembler.AssembleAsync(plan, store, _root, AssemblyOptions.Default));
    }

    [Fact]
    public async Task ReassemblesFileSpanningTwoBundles()
    {
        var c1 = new byte[] { 1, 2, 3, 4, 5 };
        var c2 = new byte[] { 6, 7, 8 };
        var bundle1 = HashId.Parse("a1");
        var bundle2 = HashId.Parse("b2");

        var store = new InMemoryBundleStore();
        store.AddBundle(bundle1, c1);
        store.AddBundle(bundle2, c2);

        var content = c1.Concat(c2).ToArray();

        var placements = new[]
        {
            new ChunkPlacement(bundle1, 0, c1.Length, Hashes.Sha1(c1)),
            new ChunkPlacement(bundle2, 0, c2.Length, Hashes.Sha1(c2)),
        };

        var file = new FileEntry("multi/bundle.bin", content.Length, Hashes.Sha1(content), [], false, null);

        var result = await _assembler.AssembleAsync(new FilePlan(file, placements), store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.Written, result.Status);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(_root, "multi", "bundle.bin")));
    }

    [Fact]
    public async Task SymlinkWithUnsafeTargetIsSkipped()
    {
        var store = new InMemoryBundleStore();
        var file = new FileEntry("link", 0, default, [], false, "../../../etc/passwd");
        var result = await _assembler.AssembleAsync(new FilePlan(file, []), store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.SymlinkUnsupported, result.Status);
    }

    [Fact]
    public async Task SymlinkWithSafeTargetIsCreatedOrReportedUnsupported()
    {
        var store = new InMemoryBundleStore();
        var file = new FileEntry("sub/link", 0, default, [], false, "target.bin");
        var result = await _assembler.AssembleAsync(new FilePlan(file, []), store, _root, AssemblyOptions.Default);

        Assert.Contains(result.Status, new[] { FileAssemblyStatus.SymlinkCreated, FileAssemblyStatus.SymlinkUnsupported });
    }

    [Fact]
    public async Task VerificationDisabledWritesEvenWithWrongHash()
    {
        var served = new byte[] { 1, 2, 3, 4 };
        var bundle = HashId.Parse("fa11");
        var store = new InMemoryBundleStore();
        store.AddBundle(bundle, served);

        var placements = new[] { new ChunkPlacement(bundle, 0, served.Length, Hashes.Label("wrong-chunk")) };
        var file = new FileEntry("noverify.bin", served.Length, Hashes.Label("wrong-file"), [], false, null);
        var options = new AssemblyOptions { VerifyChunks = false, VerifyFiles = false };

        var result = await _assembler.AssembleAsync(new FilePlan(file, placements), store, _root, options);

        Assert.Equal(FileAssemblyStatus.Written, result.Status);
        Assert.Equal(served, await File.ReadAllBytesAsync(Path.Combine(_root, "noverify.bin")));
    }

    [Fact]
    public async Task ExistingFileWithWrongSizeIsRewritten()
    {
        var content = new byte[] { 5, 6, 7, 8, 9 };
        var bundle = HashId.Parse("5a5a");
        var store = new InMemoryBundleStore();
        store.AddBundle(bundle, content);

        var path = Path.Combine(_root, "resize.bin");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(path, "\0\0"u8.ToArray());

        var placements = new[] { new ChunkPlacement(bundle, 0, content.Length, Hashes.Sha1(content)) };
        var file = new FileEntry("resize.bin", content.Length, Hashes.Sha1(content), [], false, null);

        var result = await _assembler.AssembleAsync(new FilePlan(file, placements), store, _root, AssemblyOptions.Default);

        Assert.Equal(FileAssemblyStatus.Written, result.Status);
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
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
