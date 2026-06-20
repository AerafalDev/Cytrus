using System.Diagnostics.CodeAnalysis;

namespace Cytrus.Hash;

public readonly struct HashId : IEquatable<HashId>
{
    private readonly byte[] _bytes;

    public bool IsEmpty =>
        _bytes.Length is 0;

    public ReadOnlySpan<byte> Span =>
        _bytes;

    public int Length =>
        _bytes.Length;

    public string Hex =>
        Convert.ToHexStringLower(_bytes);

    public string ShardPrefix =>
        Hex.Length >= 2 ? Hex[..2] : Hex;

    public HashId(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    public static HashId Parse(string hex)
    {
        return new HashId(Convert.FromHexString(hex));
    }

    public static HashId FromMemory(ReadOnlyMemory<byte> memory)
    {
        return new HashId(memory.Span);
    }

    public bool Equals(HashId other)
    {
        return _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is HashId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _bytes.Length < 4
            ? 0
            : BitConverter.ToInt32(_bytes, 0);
    }

    public override string ToString()
    {
        return Hex;
    }

    public static bool operator ==(HashId left, HashId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HashId left, HashId right)
    {
        return !left.Equals(right);
    }
}
