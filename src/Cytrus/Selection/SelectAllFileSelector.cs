namespace Cytrus.Selection;

public sealed class SelectAllFileSelector : IFileSelector
{
    public static SelectAllFileSelector Instance { get; } = new();

    public bool IsSelected(string fileName)
    {
        return true;
    }
}
