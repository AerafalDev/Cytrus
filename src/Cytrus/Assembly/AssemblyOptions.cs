namespace Cytrus.Assembly;

public sealed record AssemblyOptions
{
    public static AssemblyOptions Default { get; } = new();

    public bool VerifyChunks { get; init; } = true;

    public bool VerifyFiles { get; init; } = true;

    public bool SkipUpToDate { get; init; } = true;
}
