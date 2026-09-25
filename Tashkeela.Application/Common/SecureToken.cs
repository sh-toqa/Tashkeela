using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Tashkeela.Application.Common;

/// <summary>
/// Random bearer secrets (invitation codes). The plain value is handed out once; only the hash is stored (NFR-SEC-2).
/// A fast hash is fine: 256 random bits can't be brute-forced from their hash.
/// </summary>
internal static class SecureToken
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 as 64 upper-case hex characters.</summary>
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
