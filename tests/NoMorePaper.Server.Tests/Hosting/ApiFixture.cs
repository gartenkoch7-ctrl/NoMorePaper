using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Tests.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(ApiFixture))]

namespace NoMorePaper.Server.Tests.Hosting;

/// <summary>
/// The real Server in memory, with a throwaway Postgres container (same image as the UI tests) and
/// a temporary data folder. Shared by all tests of the assembly; tests use their own uploads and
/// accounts. The household is set up once (administrator <see cref="AdminUserName"/>), after a
/// document without owner was recorded, as M1 left them.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUserName = "testadmin";
    public const string AdminPassword = "admin-password-1";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-server-tests-");
    private string _adminCookie = string.Empty;

    /// <summary>The document recorded before the setup, without owner.</summary>
    public Guid LegacyDocumentId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync().ConfigureAwait(false);

        // Start the host once, here: the factory starts it lazily and unsynchronized, so parallel
        // tests would start (and migrate) it several times.
        using var client = CreateClient();
        LegacyDocumentId = await AddLegacyDocumentAsync(Services).ConfigureAwait(false);

        using var setup = await client.PostAsJsonAsync(
            "/api/setup", new SetupDto(AdminUserName, "Test Admin", AdminPassword)).ConfigureAwait(false);
        setup.EnsureSuccessStatusCode();
        _adminCookie = SessionCookie.From(setup) ?? throw new InvalidOperationException("Setup did not sign in.");
    }

    /// <summary>A client signed in as the administrator (cookie of the setup, not updated).</summary>
    public HttpClient CreateAdminClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", _adminCookie);
        return client;
    }

    /// <summary>A factory on its own, empty database: for setup and emergency tests that change the administrator.</summary>
    public WebApplicationFactory<Program> WithFreshDatabase(string name) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:NoMorePaper"] = new NpgsqlConnectionStringBuilder(_database.GetConnectionString()) { Database = name }.ConnectionString,
            })));

    /// <summary>
    /// The same Server and database, but every request checks its session against the store, so a
    /// test sees at once what production enforces within 5 minutes.
    /// </summary>
    public WebApplicationFactory<Program> WithSessionCheckOnEveryRequest() =>
        WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<AccountSessionOptions>(options => options.ValidationInterval = TimeSpan.Zero)));

    /// <summary>Records a document as M1 did: no owner, not private.</summary>
    public static async Task<Guid> AddLegacyDocumentAsync(IServiceProvider services)
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = Document.Receive("aus-m1", DateTimeOffset.UtcNow, Guid.CreateVersion7(), isPrivate: false);
            document.AttachFile(new DocumentFile(DocumentFileKind.Original, "aus-m1.pdf", "application/pdf", 1, new string('0', 64)));
            db.Documents.Add(document);
            await db.SaveChangesAsync().ConfigureAwait(false);
            await db.Documents.Where(d => d.Id == document.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.OwnerId, (Guid?)null)).ConfigureAwait(false);
            return document.Id;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configuration is read when services are first resolved, so these sources are honoured.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:NoMorePaper"] = _database.GetConnectionString(),
                ["NoMorePaper:DataPath"] = _dataRoot.FullName,
                ["NoMorePaper:DataProtectionPath"] = Path.Combine(_dataRoot.FullName, "keys"),
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
