using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Processing;

internal sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    private const int StageLength = 16;

    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        // Names are spelled out because the queue claims jobs with plain SQL.
        builder.ToTable("processing_jobs");
        builder.HasKey(job => job.DocumentId);
        builder.Property(job => job.DocumentId).HasColumnName("document_id").ValueGeneratedNever();

        // The migration turns jobs from before recognition existed into OCR jobs of priority 0.
        builder.Property(job => job.Stage).HasColumnName("stage").HasConversion<string>().HasMaxLength(StageLength);
        builder.Property(job => job.Priority).HasColumnName("priority");
        builder.Property(job => job.Attempt).HasColumnName("attempt");
        builder.Property(job => job.ClaimId).HasColumnName("claim_id");
        builder.Property(job => job.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(job => job.LockedUntil).HasColumnName("locked_until");
        builder.Property(job => job.LastError).HasColumnName("last_error");

        builder.HasOne<Document>().WithMany().HasForeignKey(job => job.DocumentId).OnDelete(DeleteBehavior.Cascade);

        // Workers look for the due job with the lowest priority number, the earliest among equals.
        builder.HasIndex(job => new { job.Priority, job.NextAttemptAt });
    }
}
