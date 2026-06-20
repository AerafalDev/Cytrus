using System.Security;
using CommunityToolkit.Diagnostics;

namespace Cytrus.Assembly;

public static class PathSafety
{
    private static readonly StringComparison s_pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string ResolveWithinRoot(string root, string relativeName)
    {
        Guard.IsNotNullOrWhiteSpace(root);
        Guard.IsNotNullOrWhiteSpace(relativeName);

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalized = relativeName.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
            throw new SecurityException($"Refusing rooted path in manifest entry: '{relativeName}'.");

        var combined = Path.GetFullPath(Path.Combine(fullRoot, normalized));

        if (!IsWithin(fullRoot, combined))
            throw new SecurityException($"Manifest entry '{relativeName}' resolves outside the output directory and was blocked.");

        return combined;
    }

    public static bool IsSymlinkTargetSafe(string root, string linkFullPath, string target)
    {
        if (string.IsNullOrEmpty(target))
            return false;

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var linkDir = Path.GetDirectoryName(linkFullPath) ?? fullRoot;
        var normalizedTarget = target.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);

        var resolved = Path.IsPathRooted(normalizedTarget)
            ? Path.GetFullPath(normalizedTarget)
            : Path.GetFullPath(Path.Combine(linkDir, normalizedTarget));

        return IsWithin(fullRoot, resolved);
    }

    private static bool IsWithin(string root, string candidate)
    {
        return string.Equals(root, candidate, s_pathComparison) || candidate.StartsWith(string.Concat(root, Path.DirectorySeparatorChar), s_pathComparison);
    }
}
