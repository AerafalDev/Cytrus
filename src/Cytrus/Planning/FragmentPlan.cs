namespace Cytrus.Planning;

public sealed record FragmentPlan(
    string FragmentName,
    IReadOnlyList<BundlePlan> Bundles,
    IReadOnlyList<FilePlan> Files)
{
    public long TotalDownloadBytes =>
        Bundles.Sum(static x => x.DownloadBytes);

    public bool IsEmpty =>
        Files.Count is 0;
}
