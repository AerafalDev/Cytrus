using Cytrus.Hash;

namespace Cytrus.Models;

public sealed record FileEntry(
    string Name,
    long Size,
    HashId Hash,
    IReadOnlyList<ChunkInfo> Chunks,
    bool Executable,
    string? Symlink)
{
    public bool IsSymlink =>
        !string.IsNullOrEmpty(Symlink);
}
