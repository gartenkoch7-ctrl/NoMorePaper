using Microsoft.EntityFrameworkCore;
using NoMorePaper.Infrastructure.Heartbeat;

namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>The application's Postgres database. Entity mappings live next to their feature.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
