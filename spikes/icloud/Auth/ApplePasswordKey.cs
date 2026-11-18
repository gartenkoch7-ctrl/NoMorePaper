using System.Security.Cryptography;
using System.Text;
using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>
/// Turns the typed password into the value Apple's sign-in uses as the SRP password:
/// PBKDF2-HMAC-SHA256 over the SHA-256 of the password, with salt and iteration count from the server.
/// </summary>
internal static class ApplePasswordKey
{
    /// <summary>The raw 32-byte digest is the PBKDF2 input.</summary>
    public const string ProtocolS2k = "s2k";

    /// <summary>The lower-case hex text of the digest (64 ASCII bytes) is the PBKDF2 input.</summary>
    public const string ProtocolS2kFo = "s2k_fo";

    private const int KeyLength = 32;

    // Guards against a nonsensical or hostile server answer; Apple's real values are far inside.
    private const int MinIterations = 1;
    private const int MaxIterations = 5_000_000;

    public static byte[] Derive(ReadOnlySpan<byte> passwordUtf8, string protocol, byte[] salt, int iterations)
    {
        if (iterations is < MinIterations or > MaxIterations)
        {
            throw new ICloudException(ICloudFailure.ProtocolChanged, "Sign-in: iteration count out of range.");
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(passwordUtf8, digest);
        byte[] input = protocol switch
        {
            ProtocolS2k => digest.ToArray(),
            ProtocolS2kFo => Encoding.ASCII.GetBytes(Convert.ToHexStringLower(digest)),
            _ => throw new ICloudException(
                ICloudFailure.ProtocolChanged, "Sign-in: server asked for an unknown password protocol."),
        };

        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(input, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(digest);
        }
    }
}
