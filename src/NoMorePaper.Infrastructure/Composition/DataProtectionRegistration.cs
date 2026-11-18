using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>Data Protection for Server and Worker: shared key folder, key ring encrypted with a certificate (ADR-0015).</summary>
public static class DataProtectionRegistration
{
    /// <remarks>
    /// The certificate is loaded here, because the framework needs it when the key ring is set up. A
    /// configured but unreadable certificate fails immediately; a missing one only outside
    /// <c>Development</c> and at host start (<see cref="KeyRingCertificateGuard"/>), so tools that
    /// only compose the services keep working.
    /// </remarks>
    public static IServiceCollection AddNoMorePaperDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Registered before AddDataProtection: hosted services start in order, and the framework's own
        // one creates the first key right at start, which must not happen without a certificate.
        services.AddHostedService<KeyRingCertificateGuard>();
        var builder = services.AddDataProtection().SetApplicationName(InfrastructureSettings.DataProtectionApplicationName);
        services.AddOptions<KeyManagementOptions>().Configure<ILoggerFactory>((options, loggerFactory) =>
        {
            // Without a path the framework default applies (user profile on a dev box).
            var path = configuration[InfrastructureSettings.DataProtectionPathKey];
            if (!string.IsNullOrWhiteSpace(path))
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), loggerFactory);
            }
        });

        var certificate = LoadCertificate(configuration);
        if (certificate is not null)
        {
            builder.ProtectKeysWithCertificate(certificate);
        }

        services.AddSingleton(new KeyRingProtection(certificate?.Thumbprint));
        services.AddSingleton<UnencryptedKeyRevocation>();
        return services;
    }

    private static X509Certificate2? LoadCertificate(IConfiguration configuration)
    {
        var path = configuration[InfrastructureSettings.KeyRingCertificatePathKey];
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var password = configuration[InfrastructureSettings.KeyRingCertificatePasswordKey];
        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(path, password);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException)
        {
            // The message never contains the password.
            throw new InvalidOperationException(
                $"The key ring certificate '{path}' (env NoMorePaper__KeyRingCertificatePath) cannot be loaded; check the file and NoMorePaper__KeyRingCertificatePassword.",
                error);
        }
    }
}
