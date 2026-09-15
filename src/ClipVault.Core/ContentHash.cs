using System.Security.Cryptography;
using System.Text;

namespace ClipVault.Core;

public static class ContentHash
{
    public static string OfText(string text) => OfBytes(Encoding.UTF8.GetBytes(text));

    public static string OfBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
