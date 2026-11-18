namespace NoMorePaper.Application.Persistence;

/// <summary>Brings the database schema up to date. Called once by the Server at startup.</summary>
public interface ISchemaMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
