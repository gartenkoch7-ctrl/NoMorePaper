using Xunit;

namespace NoMorePaper.Worker.Tests.Persistence;

/// <summary>Shares one Postgres container across all test classes that need a database.</summary>
[CollectionDefinition(Name)]
public sealed class SharedPostgres : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
