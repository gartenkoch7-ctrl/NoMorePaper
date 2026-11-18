namespace NoMorePaper.Application.Accounts;

/// <summary>Creates the secrets of the account feature from a cryptographic random source.</summary>
public interface ISecretGenerator
{
    /// <summary>A link token: 32 random bytes, Base64url (43 characters, safe in a URL path).</summary>
    string NewToken();

    /// <summary>An emergency code: <see cref="RandomSecretGenerator.EmergencyCodeLength"/> Base32 characters (100 bits).</summary>
    string NewEmergencyCode();
}
