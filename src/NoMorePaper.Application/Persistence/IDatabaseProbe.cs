namespace NoMorePaper.Application.Persistence;

/// <summary>Tells whether the database is reachable; backs the Server's health check.</summary>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
