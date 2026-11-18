using Microsoft.EntityFrameworkCore;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Heartbeat;
using NoMorePaper.Infrastructure.Processing;

namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>The application's Postgres database. Entity mappings live next to their feature.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram similarity for search despite OCR errors.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
