using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>
/// Stops the host at start when the key ring would be stored in clear text outside <c>Development</c>.
/// With a certificate, it then revokes keys from before the certificate (<see cref="UnencryptedKeyRevocation"/>),
/// before the framework loads the key ring.
/// </summary>
internal sealed partial class KeyRingCertificateGuard(
    KeyRingProtection protection,
    IHostEnvironment environment,
    UnencryptedKeyRevocation revocation,
    ILogger<KeyRingCertificateGuard> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (protection.Thumbprint is { } thumbprint)
        {
            KeyRingProtected(logger, thumbprint);
            revocation.RevokeUnencryptedKeys();
            return Task.CompletedTask;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "No key ring certificate configured: set NoMorePaper__KeyRingCertificatePath (and NoMorePaper__KeyRingCertificatePassword). " +
                "Create one with scripts/new-keyring-certificate.ps1 (ADR-0015, docs/kb/betrieb.md).");
        }

        KeyRingUnprotected(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 3400, Level = LogLevel.Information, Message = "Key ring is encrypted with the certificate {Thumbprint}")]
    private static partial void KeyRingProtected(ILogger logger, string thumbprint);

    [LoggerMessage(EventId = 3401, Level = LogLevel.Warning, Message = "No key ring certificate configured; the key ring is stored unencrypted (Development only)")]
    private static partial void KeyRingUnprotected(ILogger logger);
}
