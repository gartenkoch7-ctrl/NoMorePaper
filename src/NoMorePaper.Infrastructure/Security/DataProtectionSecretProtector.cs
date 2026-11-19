using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Security;

namespace NoMorePaper.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> with ASP.NET Data Protection and the shared, certificate-protected key
/// ring of Server and Worker (ADR-0015): what the Server stores, the Worker can read. One protector per
/// purpose, so a mail password never opens as an iCloud session.
/// </summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider, ILogger<DataProtectionSecretProtector> logger) : ISecretProtector
{
    private const string PurposePrefix = "NoMorePaper.Inputs.";

    public byte[] Protect(string purpose, string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Protector(purpose).Protect(Encoding.UTF8.GetBytes(secret));
    }

    public string Unprotect(string purpose, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            return Encoding.UTF8.GetString(Protector(purpose).Unprotect(data));
        }
        catch (CryptographicException exception)
        {
            // The framework's message may describe the key; the purpose is a fixed word.
            logger.SecretUnreadable(purpose);
            throw new SecretUnreadableException("The stored secret cannot be read; enter it again.", exception);
        }
    }

    private IDataProtector Protector(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return provider.CreateProtector(PurposePrefix + purpose);
    }
}
