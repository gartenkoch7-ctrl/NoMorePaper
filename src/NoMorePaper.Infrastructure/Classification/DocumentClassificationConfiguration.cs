using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Accounts;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// Maps the fields of <see cref="Document"/> (columns of <c>documents</c>), its tags
/// (<c>document_tags</c>) and its editors (<c>document_editors</c>). The rest of the document is
/// mapped by <c>DocumentConfiguration</c>.
/// </summary>
internal sealed class DocumentClassificationConfiguration : IEntityTypeConfiguration<Document>
{
    private const string DocumentId = "DocumentId";
    private const int EnumLength = 16;
    private const int CurrencyLength = 3;
    private const int AmountPrecision = 18;

    public void Configure(EntityTypeBuilder<Document> builder)
    {
        // Merging moves the documents before deleting the source entries; set null is the safety net.
        builder.Property(document => document.CorrespondentId).HasColumnName("correspondent_id");
        builder.HasOne<Correspondent>().WithMany().HasForeignKey(document => document.CorrespondentId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(document => document.DocumentTypeId).HasColumnName("document_type_id");
        builder.HasOne<DocumentType>().WithMany().HasForeignKey(document => document.DocumentTypeId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(document => document.DocumentDate).HasColumnName("document_date");
        builder.ComplexProperty(document => document.Amount, amount =>
        {
            amount.Property(money => money.Value).HasColumnName("amount").HasPrecision(AmountPrecision, Money.MaxDecimalPlaces);
            amount.Property(money => money.Currency).HasColumnName("currency").HasMaxLength(CurrencyLength).IsFixedLength();
        });
        builder.Property(document => document.Summary).HasColumnName("summary").HasMaxLength(DocumentFields.MaxSummaryLength);
        builder.Property(document => document.Recognition)
            .HasColumnName("recognition")
            .HasConversion<string>()
            .HasMaxLength(EnumLength);

        // Deleting the other document resolves the hint.
        builder.Property(document => document.PossibleDuplicateOf).HasColumnName("possible_duplicate_of");
        builder.HasOne<Document>().WithMany().HasForeignKey(document => document.PossibleDuplicateOf).OnDelete(DeleteBehavior.SetNull);

        // The list filters by period.
        builder.HasIndex(document => document.DocumentDate);

        builder.OwnsMany(document => document.Tags, ConfigureTags);
        builder.Navigation(document => document.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(document => document.Editors, ConfigureEditors);
        builder.Navigation(document => document.Editors).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureTags(OwnedNavigationBuilder<Document, DocumentTag> tag)
    {
        tag.ToTable("document_tags");
        tag.WithOwner().HasForeignKey(DocumentId);
        tag.Property<Guid>(DocumentId).HasColumnName("document_id");
        tag.Property(t => t.TagId).HasColumnName("tag_id");
        tag.HasKey(DocumentId, nameof(DocumentTag.TagId));
        tag.HasOne<Tag>().WithMany().HasForeignKey(t => t.TagId).OnDelete(DeleteBehavior.Cascade);
    }

    // A removed member is no editor any more.
    private static void ConfigureEditors(OwnedNavigationBuilder<Document, DocumentEditor> editor)
    {
        editor.ToTable("document_editors");
        editor.WithOwner().HasForeignKey(DocumentId);
        editor.Property<Guid>(DocumentId).HasColumnName("document_id");
        editor.Property(e => e.AccountId).HasColumnName("account_id");
        editor.HasKey(DocumentId, nameof(DocumentEditor.AccountId));
        editor.HasOne<AccountUser>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
