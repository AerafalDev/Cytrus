using Cytrus.Hash;
using FlatSharp;
using FbManifest = Cytrus.Manifests.Manifest;
using FbFragment = Cytrus.Manifests.Fragment;
using FbFile = Cytrus.Manifests.File;
using FbBundle = Cytrus.Manifests.Bundle;
using FbChunk = Cytrus.Manifests.Chunk;

namespace Cytrus.Tests.Support;

public sealed class FakeGameBuilder
{
    public const string Version = "1.0_test";

    private readonly List<FbBundle> _bundles = [];
    private readonly List<FbFile> _files = [];
    private readonly Dictionary<string, byte[]> _bundleBytes = new();
    private readonly Dictionary<string, byte[]> _expected = new();

    public IReadOnlyDictionary<string, (long offset, long size, HashId hash)> AddBundle(string bundleLabel, params (string label, byte[] bytes)[] chunks)
    {
        var body = chunks.SelectMany(c => c.bytes).ToArray();
        var bundleHash = Hashes.Sha1(body);
        var map = new Dictionary<string, (long, long, HashId)>();
        var fbChunks = new List<FbChunk>();

        long offset = 0;

        foreach (var (label, bytes) in chunks)
        {
            var hash = Hashes.Sha1(bytes);
            fbChunks.Add(new FbChunk { Hash = hash.Span.ToArray(), Size = bytes.Length, Offset = offset });
            map[label] = (offset, bytes.Length, hash);
            offset += bytes.Length;
        }

        _bundles.Add(new FbBundle { Hash = bundleHash.Span.ToArray(), Chunks = fbChunks });
        _bundleBytes[bundleHash.Hex] = body;
        return map;
    }

    public FakeGameBuilder AddFile(string name, byte[] content, params HashId[] chunkHashes)
    {
        var chunks = chunkHashes.Select(h => new FbChunk { Hash = h.Span.ToArray(), Size = 0, Offset = 0 }).ToList();
        _files.Add(new FbFile { Name = name, Size = content.Length, Hash = Hashes.Sha1(content).Span.ToArray(), Chunks = chunks });
        _expected[name] = content;
        return this;
    }

    public FakeGameBuilder AddSingleChunkFile(string name, byte[] content)
    {
        _files.Add(new FbFile { Name = name, Size = content.Length, Hash = Hashes.Sha1(content).Span.ToArray(), Chunks = [] });
        _expected[name] = content;
        return this;
    }

    public FakeGameBuilder AddEmptyFile(string name)
    {
        _files.Add(new FbFile { Name = name, Size = 0, Chunks = [] });
        _expected[name] = [];
        return this;
    }

    public FakeGameBuilder AddSymlink(string name, string target)
    {
        _files.Add(new FbFile { Name = name, Size = 0, Symlink = target, Chunks = [] });
        return this;
    }

    public (FakeCdnClient cdn, IReadOnlyDictionary<string, byte[]> expected) Build()
    {
        var manifest = new FbManifest { Fragments = [new FbFragment { Name = "frag", Bundles = _bundles, Files = _files }] };
        var buffer = new byte[FbManifest.Serializer.GetMaxSize(manifest)];
        var written = FbManifest.Serializer.Write(buffer, manifest);
        var cdn = new FakeCdnClient(buffer[..written], _bundleBytes);
        return (cdn, _expected);
    }
}
