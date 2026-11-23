using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>Maps <c>document_pages</c>; the pages go with their document.</summary>
internal sealed class DocumentPagesConfiguration : IEntityTypeConfiguration<DocumentPageRow>
{
    public void Configure(EntityTypeBuilder<DocumentPageRow> builder)
    {
        builder.ToTable("document_pages");
        builder.HasKey(row => new { row.DocumentId, row.Page });
        builder.Property(row => row.DocumentId).HasColumnName("document_id");
        builder.Property(row => row.Page).HasColumnName("page");
        builder.Property(row => row.Head).HasColumnName("head").HasMaxLength(PageInfo.MaxHeadLength);
        builder.Property(row => row.ProbablyBlank).HasColumnName("probably_blank");
        builder.Property(row => row.HadTextLayer).HasColumnName("had_text_layer");
        builder.HasOne<Document>().WithMany().HasForeignKey(row => row.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
