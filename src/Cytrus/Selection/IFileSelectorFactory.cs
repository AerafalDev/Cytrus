namespace Cytrus.Selection;

public interface IFileSelectorFactory
{
    IFileSelector Create(IReadOnlyList<string>? patterns);
}
