using CommunityToolkit.Diagnostics;
using DotNet.Globbing;

namespace Cytrus.Selection;

public sealed class GlobFileSelector : IFileSelector
{
    private readonly Glob[] _globs;

    public GlobFileSelector(IReadOnlyList<string> patterns)
    {
        Guard.IsNotNull(patterns);

        _globs = new Glob[patterns.Count];

        for (var i = 0; i < patterns.Count; i++)
            _globs[i] = Glob.Parse(Normalize(patterns[i]));
    }

    public bool IsSelected(string fileName)
    {
        var normalized = Normalize(fileName);

        return _globs.Any(glob => glob.IsMatch(normalized));
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }
}
