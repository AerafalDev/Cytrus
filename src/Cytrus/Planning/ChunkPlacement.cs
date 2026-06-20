using Cytrus.Hash;

namespace Cytrus.Planning;

public sealed record ChunkPlacement(HashId BundleHash, long Offset, long Size, HashId ChunkHash);
