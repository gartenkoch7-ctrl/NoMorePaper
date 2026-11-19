namespace NoMorePaper.Application.Security;

/// <summary>
/// Encrypts secrets of the inputs (mail password, iCloud session) for storage in Postgres with Data
/// Protection (ADR-0015); a secret is never stored, logged or returned in plain text (AGENTS.md 2.4).
/// </summary>
public interface ISecretProtector
{
    /// <param name="purpose">One of <see cref="SecretPurposes"/>; a secret only opens with the purpose it was protected for.</param>
    byte[] Protect(string purpose, string secret);

    /// <exception cref="SecretUnreadableException">The data was not protected for this purpose, is damaged, or its key is gone.</exception>
    string Unprotect(string purpose, byte[] data);
}
