using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Trash;

/// <summary>
/// Maps <c>trash_items</c>. An entry goes with its document (cascade); it has no owner column of its own,
/// the document's owner owns it, also after a hand-over.
/// </summary>
internal sealed class TrashItemConfiguration : IEntityTypeConfiguration<TrashItemRow>
{
    private const int EnumLength = 16;

    public void Configure(EntityTypeBuilder<TrashItemRow> builder)
    {
        builder.ToTable("trash_items");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.DocumentId).HasColumnName("document_id");
        builder.Property(row => row.File)
            .HasColumnName("file")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<DocumentFile?, string?>(
                    file => file == null ? null : DocumentFileJson.Write(file),
                    json => json == null ? null : DocumentFileJson.Read(json)));
        builder.Property(row => row.Title).HasColumnName("title").HasMaxLength(Document.MaxTitleLength);
        builder.Property(row => row.DeletedAt).HasColumnName("deleted_at");
        builder.Property(row => row.PurgeAfter).HasColumnName("purge_after");

        builder.HasOne<Document>().WithMany().HasForeignKey(row => row.DocumentId).OnDelete(DeleteBehavior.Cascade);

        // The daily purge looks for entries whose time is up; the trash page joins documents by id.
        builder.HasIndex(row => row.PurgeAfter);
        builder.HasIndex(row => row.DocumentId);
    }
}
