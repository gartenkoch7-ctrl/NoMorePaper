using System.Buffers.Text;
using System.Security.Cryptography;

namespace NoMorePaper.Application.Accounts;

/// <summary>Secrets from <see cref="RandomNumberGenerator"/>, the operating system's cryptographic source.</summary>
public sealed class RandomSecretGenerator : ISecretGenerator
{
    public const int TokenBytes = 32;

    public const int EmergencyCodeLength = 20;

    // RFC 4648 Base32: no 0/1/8/9, so nobody confuses O and 0 or I and 1 when typing the code.
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public string NewEmergencyCode() => RandomNumberGenerator.GetString(Base32Alphabet, EmergencyCodeLength);
}
