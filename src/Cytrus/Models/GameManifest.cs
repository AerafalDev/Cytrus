namespace Cytrus.Models;

public sealed record GameManifest(IReadOnlyList<FragmentInfo> Fragments)
{
    public long TotalFileCount =>
        Fragments.Sum(static x => (long)x.Files.Count);
}
