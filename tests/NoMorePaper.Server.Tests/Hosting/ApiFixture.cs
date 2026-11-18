using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NoMorePaper.Server.Tests.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(ApiFixture))]

namespace NoMorePaper.Server.Tests.Hosting;

/// <summary>
/// The real Server in memory, with a throwaway Postgres container (same image as the UI tests) and
/// a temporary data folder. Shared by all tests of the assembly; tests use their own uploads.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-server-tests-");

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync().ConfigureAwait(false);

        // Start the host once, here: the factory starts it lazily and unsynchronized, so parallel
        // tests would start (and migrate) it several times.
        using var client = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configuration is read when services are first resolved, so these sources are honoured.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:NoMorePaper"] = _database.GetConnectionString(),
                ["NoMorePaper:DataPath"] = _dataRoot.FullName,
            }));
    }

    /// <summary>Clients act like scripts outside a browser: no Sec-Fetch-Site, but the request marker.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-NoMorePaper-Request", "1");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        await _database.DisposeAsync().ConfigureAwait(false);
        _dataRoot.Delete(recursive: true);
    }
}
