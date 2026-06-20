using Cytrus.Assembly;
using Cytrus.Models;
using Cytrus.Planning;

namespace Cytrus.Download;

public sealed record DownloadRequest
{
    public required GameCoordinates Coordinates { get; init; }

    public required string OutputDirectory { get; init; }

    public IReadOnlyList<string>? Select { get; init; }

    public AssemblyOptions Assembly { get; init; } = AssemblyOptions.Default;

    public PlannerOptions Planner { get; init; } = PlannerOptions.Default;

    public int? MaxParallelDownloads { get; init; }

    public IDownloadProgressSink? Progress { get; init; }
}
