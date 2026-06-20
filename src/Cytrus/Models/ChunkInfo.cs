using Cytrus.Hash;

namespace Cytrus.Models;

public sealed record ChunkInfo(HashId Hash, long Size, long Offset);
