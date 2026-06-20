using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Selection;

namespace Cytrus.Planning;

public sealed class DownloadPlanner : IDownloadPlanner
{
    public FragmentPlan Plan(FragmentInfo fragment, IFileSelector selector, PlannerOptions options)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(options);

        var chunkIndex = BuildChunkIndex(fragment);

        var filePlans = new List<FilePlan>();
        var neededByBundle = new Dictionary<HashId, Dictionary<long, long>>();

        foreach (var file in fragment.Files)
        {
            if (!selector.IsSelected(file.Name))
                continue;

            if (file.IsSymlink || file.Size is 0)
            {
                filePlans.Add(new FilePlan(file, []));
                continue;
            }

            var placements = ResolvePlacements(file, chunkIndex);

            filePlans.Add(new FilePlan(file, placements));

            foreach (var placement in placements)
            {
                if (!neededByBundle.TryGetValue(placement.BundleHash, out var intervals))
                    neededByBundle[placement.BundleHash] = intervals = new Dictionary<long, long>();

                intervals[placement.Offset] = placement.Size;
            }
        }

        var bundlePlans = new List<BundlePlan>(neededByBundle.Count);

        foreach (var (bundleHash, intervals) in neededByBundle)
            bundlePlans.Add(new BundlePlan(bundleHash, CoalesceRanges(intervals, options.CoalesceGapThreshold)));

        return new FragmentPlan(fragment.Name, bundlePlans, filePlans);
    }

    private static Dictionary<HashId, ChunkPlacement> BuildChunkIndex(FragmentInfo fragment)
    {
        var index = new Dictionary<HashId, ChunkPlacement>();

        foreach (var bundle in fragment.Bundles)
            foreach (var chunk in bundle.Chunks)
                index.TryAdd(chunk.Hash, new ChunkPlacement(bundle.Hash, chunk.Offset, chunk.Size, chunk.Hash));

        return index;
    }

    private static ChunkPlacement[] ResolvePlacements(
        FileEntry file,
        Dictionary<HashId, ChunkPlacement> chunkIndex)
    {
        if (file.Chunks.Count is 0)
        {
            if (!chunkIndex.TryGetValue(file.Hash, out var placement))
                throw new InvalidDataException($"Manifest inconsistency: no bundle contains chunk {file.Hash.Hex} for file '{file.Name}'.");

            return [placement];
        }

        var placements = new ChunkPlacement[file.Chunks.Count];

        for (var i = 0; i < file.Chunks.Count; i++)
        {
            var chunk = file.Chunks[i];

            if (!chunkIndex.TryGetValue(chunk.Hash, out var placement))
                throw new InvalidDataException($"Manifest inconsistency: no bundle contains chunk {chunk.Hash.Hex} for file '{file.Name}'.");

            placements[i] = placement;
        }

        return placements;
    }

    internal static IReadOnlyList<ByteRange> CoalesceRanges(Dictionary<long, long> intervals, long gapThreshold)
    {
        var starts = new long[intervals.Count];

        intervals.Keys.CopyTo(starts, 0);
        Array.Sort(starts);

        var ranges = new List<ByteRange>();
        var currentStart = starts[0];
        var currentEnd = starts[0] + intervals[starts[0]];

        for (var i = 1; i < starts.Length; i++)
        {
            var start = starts[i];
            var end = start + intervals[start];

            if (start <= currentEnd + gapThreshold)
            {
                if (end > currentEnd)
                    currentEnd = end;
            }
            else
            {
                ranges.Add(new ByteRange(currentStart, currentEnd));
                currentStart = start;
                currentEnd = end;
            }
        }

        ranges.Add(new ByteRange(currentStart, currentEnd));
        return ranges;
    }
}
