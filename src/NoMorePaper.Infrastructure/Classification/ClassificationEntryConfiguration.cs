using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// Maps the three lists to <c>correspondents</c>, <c>document_types</c> and <c>tags</c>, all alike. Names
/// are spelled out because <see cref="VisibilitySql"/> uses plain SQL on them.
/// </summary>
internal sealed class ClassificationEntryConfiguration :
    IEntityTypeConfiguration<Correspondent>,
    IEntityTypeConfiguration<DocumentType>,
    IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Correspondent> builder) => Map(builder, "correspondents");

    public void Configure(EntityTypeBuilder<DocumentType> builder) => Map(builder, "document_types");

    public void Configure(EntityTypeBuilder<Tag> builder) => Map(builder, "tags");

    private static void Map<TEntry>(EntityTypeBuilder<TEntry> builder, string table)
        where TEntry : ClassificationEntry
    {
        builder.ToTable(table);
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.Name).HasColumnName("name").HasMaxLength(ClassificationName.MaxLength);

        // Unbounded: decomposition may make it longer than the name ("ß" becomes "ss").
        builder.Property(entry => entry.NormalizedName).HasColumnName("normalized_name");

        // No foreign key to users, like documents.owner_id: entries outlive a removed member.
        builder.Property(entry => entry.CreatedBy).HasColumnName("created_by");

        // Finding an entry by name looks up the normalized name. Not unique: two accounts may each
        // have a private entry of the same name without learning of the other one.
        builder.HasIndex(entry => entry.NormalizedName);
    }
}
