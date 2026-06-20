namespace Cytrus.Selection;

public sealed class GlobFileSelectorFactory : IFileSelectorFactory
{
    public IFileSelector Create(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count is 0)
            return SelectAllFileSelector.Instance;

        return new GlobFileSelector(patterns);
    }
}
