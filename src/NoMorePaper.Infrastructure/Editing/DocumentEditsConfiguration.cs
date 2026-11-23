using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>Maps <c>document_edits</c>: one planned edit per document, gone with its document.</summary>
internal sealed class DocumentEditsConfiguration : IEntityTypeConfiguration<DocumentEditRow>
{
    private const int EnumLength = 16;

    public void Configure(EntityTypeBuilder<DocumentEditRow> builder)
    {
        builder.ToTable("document_edits");
        builder.HasKey(row => row.DocumentId);
        builder.Property(row => row.DocumentId).HasColumnName("document_id").ValueGeneratedNever();
        builder.Property(row => row.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.Data).HasColumnName("data").HasColumnType("jsonb");
        builder.Property(row => row.RequestedBy).HasColumnName("requested_by");
        builder.Property(row => row.RequestedAt).HasColumnName("requested_at");
        builder.Property(row => row.RecognizeAfter).HasColumnName("recognize_after");
        builder.HasOne<Document>().WithMany().HasForeignKey(row => row.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
