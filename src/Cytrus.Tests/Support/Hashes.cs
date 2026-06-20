using System.Security.Cryptography;
using System.Text;
using Cytrus.Hash;

namespace Cytrus.Tests.Support;

public static class Hashes
{
    public static HashId Sha1(ReadOnlySpan<byte> data)
    {
        return new HashId(SHA1.HashData(data));
    }

    public static HashId Label(string label)
    {
        return new HashId(SHA1.HashData(Encoding.UTF8.GetBytes(label)));
    }
}
