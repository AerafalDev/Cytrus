using Cytrus.Hash;
using Cytrus.Models;
using FlatSharp;

using Fb = Cytrus.Manifests;

namespace Cytrus.Manifest;

public sealed class FlatSharpManifestReader : IManifestReader
{
    public GameManifest Read(byte[] manifestBytes)
    {
        ArgumentNullException.ThrowIfNull(manifestBytes);

        if (manifestBytes.Length is 0)
            throw new ArgumentException("Manifest buffer is empty.", nameof(manifestBytes));

        Fb.Manifest root;

        try
        {
            root = Fb.Manifest.Serializer.Parse(new ArrayInputBuffer(manifestBytes));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("The manifest is not a valid FlatBuffers buffer.", ex);
        }

        var fbFragments = root.Fragments;

        if (fbFragments is null || fbFragments.Count is 0)
            return new GameManifest([]);

        var fragments = fbFragments.Select(MapFragment).ToList();

        return new GameManifest(fragments);
    }

    private static FragmentInfo MapFragment(Fb.Fragment fragment)
    {
        var files = MapList(fragment.Files, MapFile);
        var bundles = MapList(fragment.Bundles, MapBundle);

        return new FragmentInfo(fragment.Name ?? string.Empty, files, bundles);
    }

    private static FileEntry MapFile(Fb.File file)
    {
        var chunks = MapList(file.Chunks, MapChunk);

        return new FileEntry(
            Name: file.Name ?? string.Empty,
            Size: file.Size,
            Hash: HashId.FromMemory(file.Hash),
            Chunks: chunks,
            Executable: file.Executable,
            Symlink: string.IsNullOrEmpty(file.Symlink) ? null : file.Symlink);
    }

    private static BundleInfo MapBundle(Fb.Bundle bundle)
    {
        return new BundleInfo(HashId.FromMemory(bundle.Hash), MapList(bundle.Chunks, MapChunk));
    }

    private static ChunkInfo MapChunk(Fb.Chunk chunk) =>
        new(HashId.FromMemory(chunk.Hash), chunk.Size, chunk.Offset);

    private static TOut[] MapList<TIn, TOut>(IList<TIn>? source, Func<TIn, TOut> map)
    {
        if (source is null || source.Count is 0)
            return [];

        var result = new TOut[source.Count];

        for (var i = 0; i < source.Count; i++)
            result[i] = map(source[i]);

        return result;
    }
}
