using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;
using Cytrus.Selection;
using Cytrus.Tests.Support;

namespace Cytrus.Tests.Unit;

public sealed class DownloadPlannerTests
{
    private static readonly HashId BundleA = Hashes.Label("bundleA");
    private static readonly HashId BundleB = Hashes.Label("bundleB");
    private static readonly HashId ChA = Hashes.Label("chunkA");
    private static readonly HashId ChB = Hashes.Label("chunkB");
    private static readonly HashId ChC = Hashes.Label("chunkC");

    private readonly DownloadPlanner _planner = new();

    private static ChunkInfo Chunk(HashId h, long size, long offset)
    {
        return new ChunkInfo(h, size, offset);
    }

    private static FragmentInfo Fragment(IReadOnlyList<BundleInfo> bundles, IReadOnlyList<FileEntry> files)
    {
        return new FragmentInfo("frag", files, bundles);
    }

    [Fact]
    public void SelectsOnlyMatchingFiles()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0), Chunk(ChB, 10, 10)]);
        var keep = new FileEntry("keep.bin", 10, default, [Chunk(ChA, 10, 0)], false, null);
        var drop = new FileEntry("drop.bin", 10, default, [Chunk(ChB, 10, 10)], false, null);
        var plan = _planner.Plan(Fragment([bundle], [keep, drop]), new GlobFileSelector(["**/keep.bin"]), PlannerOptions.Default);

        var file = Assert.Single(plan.Files);
        Assert.Equal("keep.bin", file.File.Name);
    }

    [Fact]
    public void SymlinkFileHasNoChunksAndNoBundles()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0)]);
        var link = new FileEntry("link", 0, default, [], false, "target.bin");
        var plan = _planner.Plan(Fragment([bundle], [link]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        Assert.Empty(Assert.Single(plan.Files).Chunks);
        Assert.Empty(plan.Bundles);
    }

    [Fact]
    public void ZeroSizeFileHasNoChunks()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0)]);
        var empty = new FileEntry("empty", 0, default, [], false, null);
        var plan = _planner.Plan(Fragment([bundle], [empty]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        Assert.Empty(Assert.Single(plan.Files).Chunks);
        Assert.Empty(plan.Bundles);
    }

    [Fact]
    public void SingleChunkFileIsResolvedByFileHash()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 30, 0)]);
        var file = new FileEntry("blob.bin", 30, ChA, [], false, null);
        var plan = _planner.Plan(Fragment([bundle], [file]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        var placement = Assert.Single(Assert.Single(plan.Files).Chunks);
        Assert.Equal(BundleA, placement.BundleHash);
        Assert.Equal(0, placement.Offset);
        Assert.Equal(30, placement.Size);

        var bundlePlan = Assert.Single(plan.Bundles);
        Assert.Equal(new ByteRange(0, 30), Assert.Single(bundlePlan.Ranges));
    }

    [Fact]
    public void MultiChunkFileKeepsChunkOrderAndCoalescesRanges()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0), Chunk(ChB, 20, 10)]);
        var file = new FileEntry("f.bin", 30, default, [Chunk(ChA, 10, 0), Chunk(ChB, 20, 10)], false, null);
        var plan = _planner.Plan(Fragment([bundle], [file]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        var placements = Assert.Single(plan.Files).Chunks;
        Assert.Equal(2, placements.Count);
        Assert.Equal(ChA, placements[0].ChunkHash);
        Assert.Equal(ChB, placements[1].ChunkHash);

        Assert.Equal(new ByteRange(0, 30), Assert.Single(Assert.Single(plan.Bundles).Ranges));
    }

    [Fact]
    public void MissingChunkThrows()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0)]);
        var file = new FileEntry("f.bin", 10, default, [Chunk(ChC, 10, 0)], false, null); // ChC not in any bundle
        Assert.Throws<InvalidDataException>(() => _planner.Plan(Fragment([bundle], [file]), SelectAllFileSelector.Instance, PlannerOptions.Default));
    }

    [Fact]
    public void SharedChunkIsDownloadedOnce()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0)]);
        var f1 = new FileEntry("one.bin", 10, default, [Chunk(ChA, 10, 0)], false, null);
        var f2 = new FileEntry("two.bin", 10, default, [Chunk(ChA, 10, 0)], false, null);
        var plan = _planner.Plan(Fragment([bundle], [f1, f2]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        var bundlePlan = Assert.Single(plan.Bundles);
        Assert.Equal(new ByteRange(0, 10), Assert.Single(bundlePlan.Ranges));
        Assert.Equal(10, bundlePlan.DownloadBytes);
    }

    [Fact]
    public void BundlesWithoutNeededChunksAreExcluded()
    {
        var a = new BundleInfo(BundleA, [Chunk(ChA, 10, 0)]);
        var b = new BundleInfo(BundleB, [Chunk(ChB, 10, 0)]);
        var file = new FileEntry("f.bin", 10, default, [Chunk(ChA, 10, 0)], false, null);
        var plan = _planner.Plan(Fragment([a, b], [file]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        Assert.Equal(BundleA, Assert.Single(plan.Bundles).BundleHash);
    }

    [Fact]
    public void LargeGapProducesTwoRanges()
    {
        var bundle = new BundleInfo(BundleA, [Chunk(ChA, 10, 0), Chunk(ChB, 10, 1_000_000)]);
        var file = new FileEntry("f.bin", 20, default, [Chunk(ChA, 10, 0), Chunk(ChB, 10, 1_000_000)], false, null);
        var plan = _planner.Plan(Fragment([bundle], [file]), SelectAllFileSelector.Instance, PlannerOptions.Default);

        Assert.Equal(2, Assert.Single(plan.Bundles).Ranges.Count);
    }
}
