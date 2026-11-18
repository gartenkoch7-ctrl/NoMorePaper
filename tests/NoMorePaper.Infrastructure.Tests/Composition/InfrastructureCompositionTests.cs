using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Documents;
using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Infrastructure.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Composition;

public sealed class InfrastructureCompositionTests
{
    [Fact]
    public void DataProtection_PersistsKeys_SoAnotherProcessCanUnprotect()
    {
        var keys = Directory.CreateTempSubdirectory("nomorepaper-keys-");
        try
        {
            var configuration = Configuration(new() { [InfrastructureSettings.DataProtectionPathKey] = keys.FullName });

            string protectedValue;
            using (var first = Provider(configuration))
            {
                protectedValue = Protector(first).Protect("payload");
            }

            // A fresh container (restart, or Server vs. Worker) only shares the key folder.
            using var second = Provider(configuration);
            Assert.Equal("payload", Protector(second).Unprotect(protectedValue));
            Assert.NotEmpty(keys.GetFiles("key-*.xml"));
        }
        finally
        {
            keys.Delete(recursive: true);
        }
    }

    [Fact]
    public void DbContext_FailsWithAClearMessage_WhenTheConnectionStringIsMissing()
    {
        using var provider = Provider(Configuration([]));
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<AppDbContext>());

        Assert.Contains("ConnectionStrings__NoMorePaper", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DbContext_ReadsTheConnectionStringWhenResolved_NotWhenRegistered()
    {
        var configuration = new ConfigurationManager();
        using var provider = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();

        // Test hosts add configuration after the composition root ran.
        configuration["ConnectionStrings:NoMorePaper"] = "Host=localhost;Database=late";
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Contains("late", db.Database.GetConnectionString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentStorage_WritesBelowTheConfiguredDataPath()
    {
        var data = Directory.CreateTempSubdirectory("nomorepaper-data-");
        try
        {
            using var provider = Provider(Configuration(new() { [InfrastructureSettings.DataPathKey] = data.FullName }));
            var storage = provider.GetRequiredService<IDocumentStorage>();
            var id = Guid.CreateVersion7();

            await storage.SaveInboxAsync(id, "a.pdf", new MemoryStream([1]), TestContext.Current.CancellationToken);

            Assert.True(File.Exists(Path.Combine(data.FullName, "inbox", id.ToString(), "original.pdf")));
            Assert.Same(storage, provider.GetRequiredService<IDocumentStorage>());
        }
        finally
        {
            data.Delete(recursive: true);
        }
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider Provider(IConfiguration configuration) =>
        new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();

    private static IDataProtector Protector(IServiceProvider provider) =>
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("NoMorePaper.Tests");
}
