using System.Security.Cryptography;
using System.Text;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// Link tokens and emergency codes are stored only as SHA-256: they carry at least 100 random bits,
/// so a fast hash suffices, and a stolen database reveals no usable link or code. Pure logic.
/// </summary>
public static class SecretHash
{
    /// <summary>Length of <see cref="Of"/>: 64 lowercase hex characters.</summary>
    public const int Length = 64;

    /// <summary>SHA-256 of the UTF-8 bytes, as lowercase hex.</summary>
    public static string Of(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    /// <summary>
    /// Compares two hashes or other ASCII secrets (e.g. security stamps) in constant time for equal
    /// lengths, so the time taken reveals nothing about their content.
    /// </summary>
    public static bool AreEqual(string? expected, string? actual)
    {
        if (expected is null || actual is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));
    }

    /// <summary>
    /// An emergency code as typed: case, spaces and dashes do not matter, so a code read out or
    /// written down in groups still works.
    /// </summary>
    public static string NormalizeEmergencyCode(string? code) =>
        string.Concat((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
}
