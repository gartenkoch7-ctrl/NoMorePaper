using NoMorePaper.Application.Persistence;

namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>Asks Postgres whether it accepts a connection.</summary>
public sealed class DatabaseProbe(AppDbContext db) : IDatabaseProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        db.Database.CanConnectAsync(cancellationToken);
}
