namespace Cytrus.Models;

public sealed record FragmentInfo(string Name, IReadOnlyList<FileEntry> Files, IReadOnlyList<BundleInfo> Bundles);
