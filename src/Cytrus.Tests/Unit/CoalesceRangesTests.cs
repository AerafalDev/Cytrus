using Cytrus.Planning;

namespace Cytrus.Tests.Unit;

public sealed class CoalesceRangesTests
{
    [Fact]
    public void ContiguousChunksMergeIntoOneRange()
    {
        var intervals = new Dictionary<long, long> { [0] = 10, [10] = 10, [20] = 10 };
        var ranges = DownloadPlanner.CoalesceRanges(intervals, gapThreshold: 0);

        var range = Assert.Single(ranges);
        Assert.Equal(0, range.Start);
        Assert.Equal(30, range.EndExclusive);
        Assert.Equal(30, range.Length);
    }

    [Fact]
    public void DistantChunksStaySeparateWhenGapExceedsThreshold()
    {
        var intervals = new Dictionary<long, long> { [0] = 10, [1000] = 10 };
        var ranges = DownloadPlanner.CoalesceRanges(intervals, gapThreshold: 100);

        Assert.Equal(2, ranges.Count);
        Assert.Equal(new ByteRange(0, 10), ranges[0]);
        Assert.Equal(new ByteRange(1000, 1010), ranges[1]);
    }

    [Fact]
    public void SmallGapWithinThresholdIsBridged()
    {
        var intervals = new Dictionary<long, long> { [0] = 10, [60] = 10 };
        var ranges = DownloadPlanner.CoalesceRanges(intervals, gapThreshold: 64);

        var range = Assert.Single(ranges);
        Assert.Equal(new ByteRange(0, 70), range);
    }

    [Fact]
    public void UnsortedInputIsHandled()
    {
        var intervals = new Dictionary<long, long> { [20] = 10, [0] = 10, [10] = 10 };
        var ranges = DownloadPlanner.CoalesceRanges(intervals, gapThreshold: 0);
        Assert.Equal(new ByteRange(0, 30), Assert.Single(ranges));
    }

    [Fact]
    public void InclusiveEndIsOneLessThanExclusive()
    {
        Assert.Equal(29, new ByteRange(0, 30).InclusiveEnd);
    }
}
