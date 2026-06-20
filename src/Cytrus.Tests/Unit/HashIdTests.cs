using Cytrus.Hash;

namespace Cytrus.Tests.Unit;

public sealed class HashIdTests
{
    [Fact]
    public void HexIsLowercaseAndRoundtrips()
    {
        var bytes = new byte[] { 0x3F, 0xA2, 0x91, 0x0B };
        var id = new HashId(bytes);

        Assert.Equal("3fa2910b", id.Hex);
        Assert.Equal(id, HashId.Parse("3fa2910b"));
    }

    [Fact]
    public void ShardPrefixIsFirstTwoHexChars()
    {
        var id = HashId.Parse("3fa291fefbf691fcac607462b953dd7292178af3");
        Assert.Equal("3f", id.ShardPrefix);
    }

    [Fact]
    public void EqualityIsValueBasedAndUsableAsKey()
    {
        var a = HashId.Parse("deadbeefcafe");
        var b = HashId.Parse("deadbeefcafe");
        var c = HashId.Parse("deadbeefcaff");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);

        var dict = new Dictionary<HashId, int> { [a] = 1 };
        Assert.True(dict.ContainsKey(b));
        Assert.False(dict.ContainsKey(c));
    }

    [Fact]
    public void DefaultAndEmptyAreEmpty()
    {
        Assert.True(default(HashId).IsEmpty);
        Assert.True(HashId.FromMemory(null).IsEmpty);
        Assert.Equal(string.Empty, default(HashId).Hex);
    }
}
