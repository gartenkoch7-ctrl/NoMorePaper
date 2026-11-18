using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Persistence;

namespace NoMorePaper.Infrastructure.Tests.Accounts;

/// <summary>
/// A migrated database of its own with the account store as the Server registers it. Every
/// <see cref="Store"/> and <see cref="Db"/> call opens a new scope, like a new request.
/// </summary>
internal sealed class AccountDatabase : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly List<AsyncServiceScope> _scopes = [];

    private AccountDatabase(ServiceProvider provider) => _provider = provider;

    public static async Task<AccountDatabase> CreateAsync(PostgresFixture postgres, string name)
    {
        var connectionString = postgres.ConnectionString(name);
        var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString))
            .AddAccountStore()
            .BuildServiceProvider();
        var database = new AccountDatabase(provider);
        await new DatabaseMigrator(database.Db(), TimeProvider.System, NullLogger<DatabaseMigrator>.Instance)
            .MigrateAsync(CancellationToken.None);
        return database;
    }

    public IAccountStore Store() => Scope().GetRequiredService<IAccountStore>();

    public AppDbContext Db() => Scope().GetRequiredService<AppDbContext>();

    public async Task<Account> AddAdminAsync(string userName = "admin", string password = "admin-passphrase")
    {
        var result = await Store().SetUpAsync(new NewAccount(userName, "Admin", password), SecretHash.Of("CODE"), CancellationToken.None);
        return result.Value?.Administrator ?? throw new InvalidOperationException($"Setup failed: {result.Status}");
    }

    /// <summary>Creates a member through an invitation, the only way members come to be.</summary>
    public async Task<Account> AddMemberAsync(Guid adminId, string userName, string password = "member-passphrase")
    {
        var tokenHash = SecretHash.Of(Guid.NewGuid().ToString());
        await Store().AddInvitationAsync(tokenHash, adminId, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);
        var result = await Store().AcceptInvitationAsync(tokenHash, DateTimeOffset.UtcNow, new NewAccount(userName, userName, password), CancellationToken.None);
        return result.Value ?? throw new InvalidOperationException($"Accept failed: {result.Status}");
    }

    /// <summary>A document as M1 recorded it: without owner.</summary>
    public async Task<Guid> AddDocumentWithoutOwnerAsync()
    {
        var db = Db();
        var document = Document.Receive("alt", DateTimeOffset.UtcNow, Guid.CreateVersion7(), isPrivate: false);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        await db.Documents.Where(d => d.Id == document.Id).ExecuteUpdateAsync(setters => setters.SetProperty(d => d.OwnerId, (Guid?)null));
        return document.Id;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            await scope.DisposeAsync();
        }

        await _provider.DisposeAsync();
    }

    private IServiceProvider Scope()
    {
        var scope = _provider.CreateAsyncScope();
        _scopes.Add(scope);
        return scope.ServiceProvider;
    }
}
