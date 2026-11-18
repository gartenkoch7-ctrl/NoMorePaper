using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Accounts;
using NoMorePaper.Infrastructure.Heartbeat;
using NoMorePaper.Infrastructure.Processing;

namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>
/// The application's Postgres database, including the account tables of ASP.NET Identity (users only,
/// no roles: the administrator is a column). Entity mappings live next to their feature.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<AccountUser, Guid>(options)
{
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<AdminPasswordReset> AdminPasswordResets => Set<AdminPasswordReset>();

    // Pinned, so the model is the same at design time (migrations) and at run time, whatever the
    // Identity options say: version 1 has no passkey table (sign-in is user name and password only).
    protected override Version SchemaVersion => IdentitySchemaVersions.Version1;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's model first; the configurations of this assembly then rename its tables.
        base.OnModelCreating(builder);

        // Trigram similarity for search despite OCR errors.
        builder.HasPostgresExtension("pg_trgm");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
