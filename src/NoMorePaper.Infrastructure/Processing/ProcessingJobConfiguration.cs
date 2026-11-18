using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Processing;

internal sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        // Names are spelled out because the queue claims jobs with plain SQL.
        builder.ToTable("processing_jobs");
        builder.HasKey(job => job.DocumentId);
        builder.Property(job => job.DocumentId).HasColumnName("document_id").ValueGeneratedNever();
        builder.Property(job => job.Attempt).HasColumnName("attempt");
        builder.Property(job => job.ClaimId).HasColumnName("claim_id");
        builder.Property(job => job.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(job => job.LockedUntil).HasColumnName("locked_until");
        builder.Property(job => job.LastError).HasColumnName("last_error");

        builder.HasOne<Document>().WithMany().HasForeignKey(job => job.DocumentId).OnDelete(DeleteBehavior.Cascade);

        // Workers look for the earliest due job.
        builder.HasIndex(job => job.NextAttemptAt);
    }
}
