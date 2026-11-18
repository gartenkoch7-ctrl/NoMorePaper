using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NoMorePaper.Infrastructure.Composition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Composition;

public sealed class DataProtectionTests : IDisposable
{
    private const string Password = "test-only-password";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("nomorepaper-keyring-");

    private DirectoryInfo KeyFolder => Directory.CreateDirectory(Path.Combine(_folder.FullName, "keys"));

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void KeyRing_IsEncryptedOnDisk_AndReadableWithTheCertificate()
    {
        var settings = Settings(CreateCertificate("keyring.pfx"));

        string protectedValue;
        using (var first = Provider(settings))
        {
            protectedValue = Protector(first).Protect("payload");
        }

        var keyFile = Assert.Single(KeyFolder.GetFiles("key-*.xml"));
        var xml = File.ReadAllText(keyFile.FullName);
        Assert.DoesNotContain("<masterKey", xml, StringComparison.Ordinal);
        Assert.Contains("encryptedSecret", xml, StringComparison.Ordinal);

        using var second = Provider(settings);
        Assert.Equal("payload", Protector(second).Unprotect(protectedValue));
    }

    [Fact]
    public void KeyRing_IsUnreadable_WithAnotherCertificate()
    {
        var settings = Settings(CreateCertificate("a.pfx"));
        string protectedValue;
        using (var first = Provider(settings))
        {
            protectedValue = Protector(first).Protect("payload");
        }

        settings[InfrastructureSettings.KeyRingCertificatePathKey] = CreateCertificate("b.pfx");
        using var other = Provider(settings);

        Assert.ThrowsAny<CryptographicException>(() => Protector(other).Unprotect(protectedValue));
    }

    [Fact]
    public async Task Start_FailsWithAClearMessage_WithoutCertificateOutsideDevelopment()
    {
        using var provider = Provider(Settings(certificatePath: null), Environments.Production);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => StartHostedServices(provider));

        Assert.Contains("NoMorePaper__KeyRingCertificatePath", error.Message, StringComparison.Ordinal);

        // The framework's own start-up task would have written the first key in clear text.
        Assert.Empty(KeyFolder.GetFiles());
    }

    [Fact]
    public async Task Start_Succeeds_WithoutCertificateInDevelopment()
    {
        using var provider = Provider(Settings(certificatePath: null), Environments.Development);

        await StartHostedServices(provider);
    }

    [Fact]
    public async Task Start_Succeeds_WithCertificateInProduction()
    {
        using var provider = Provider(Settings(CreateCertificate("keyring.pfx")), Environments.Production);

        await StartHostedServices(provider);
    }

    [Fact]
    public async Task Start_WithCertificate_RevokesTheUnencryptedKeysFromBefore_Once()
    {
        string oldCookie;
        using (var before = Provider(Settings(certificatePath: null)))
        {
            oldCookie = Protector(before).Protect("payload");
        }

        var settings = Settings(CreateCertificate("keyring.pfx"));
        using (var after = Provider(settings, Environments.Production))
        {
            await StartHostedServices(after);

            Assert.ThrowsAny<CryptographicException>(() => Protector(after).Unprotect(oldCookie));
            Assert.Equal("new", Protector(after).Unprotect(Protector(after).Protect("new")));
        }

        using (var restarted = Provider(settings, Environments.Production))
        {
            await StartHostedServices(restarted);
        }

        var keys = KeyFolder.GetFiles("key-*.xml").Select(file => File.ReadAllText(file.FullName)).ToList();
        Assert.Equal(2, keys.Count);
        Assert.Single(keys, xml => xml.Contains("encryptedSecret", StringComparison.Ordinal));
        Assert.Single(KeyFolder.GetFiles("revocation-*.xml"));
    }

    [Fact]
    public void Composition_FailsWithAClearMessage_WhenTheCertificateCannotBeLoaded()
    {
        var path = Path.Combine(_folder.FullName, "missing.pfx");

        var error = Assert.Throws<InvalidOperationException>(() => Provider(Settings(path)));

        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, error.Message, StringComparison.Ordinal);
    }

    private static async Task StartHostedServices(ServiceProvider provider)
    {
        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(TestContext.Current.CancellationToken);
        }
    }

    private string CreateCertificate(string fileName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test key ring", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(_folder.FullName, fileName);
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, Password));
        return path;
    }

    private Dictionary<string, string?> Settings(string? certificatePath)
    {
        var settings = new Dictionary<string, string?> { [InfrastructureSettings.DataProtectionPathKey] = KeyFolder.FullName };
        if (certificatePath is not null)
        {
            settings[InfrastructureSettings.KeyRingCertificatePathKey] = certificatePath;
            settings[InfrastructureSettings.KeyRingCertificatePasswordKey] = Password;
        }

        return settings;
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings, string environment = "Development")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new FakeEnvironment(environment))
            .AddNoMorePaperDataProtection(configuration)
            .BuildServiceProvider();
    }

    private static IDataProtector Protector(ServiceProvider provider) =>
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
