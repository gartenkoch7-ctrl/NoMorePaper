using Microsoft.EntityFrameworkCore;
using NoMorePaper.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace NoMorePaper.Worker.Tests.Persistence;

/// <summary>
/// One throwaway Postgres container (same major version as docker-compose.yml) for all database
/// tests. Each test asks for its own database name, so tests never see each other's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    /// <summary>Connection string to a database that does not exist yet; migrating creates it.</summary>
    public string ConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    public AppDbContext CreateContext(string database) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString(database)).Options);
}
