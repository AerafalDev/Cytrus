using Cytrus.Models;

namespace Cytrus.Planning;

public sealed record FilePlan(FileEntry File, IReadOnlyList<ChunkPlacement> Chunks);
