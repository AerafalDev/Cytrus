using Cytrus.Hash;

namespace Cytrus.Models;

public sealed record BundleInfo(HashId Hash, IReadOnlyList<ChunkInfo> Chunks);
