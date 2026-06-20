namespace Cytrus.Planning;

public sealed record PlannerOptions
{
    public static PlannerOptions Default { get; } = new();

    public long CoalesceGapThreshold { get; init; } = 256 * 1024;
}
