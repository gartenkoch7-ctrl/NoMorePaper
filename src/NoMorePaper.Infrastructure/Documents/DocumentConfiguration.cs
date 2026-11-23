using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Persistence;
using NpgsqlTypes;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Maps <see cref="Document"/> to <c>documents</c> and its files to <c>document_files</c>. Names are
/// spelled out because search and queue use plain SQL against these tables.
/// </summary>
internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <summary>Shadow property of the generated <c>search_vector</c> column.</summary>
    public const string SearchVectorProperty = "SearchVector";

    // German and English stems over the text, the title weighted highest (task 1 of the M1 plan).
    // Generated columns need immutable expressions, hence the fixed regconfig values.
    private const string SearchVectorSql =
        "setweight(to_tsvector('german'::regconfig, coalesce(title, '')), 'A')"
        + " || to_tsvector('german'::regconfig, coalesce(content_text, ''))"
        + " || to_tsvector('english'::regconfig, coalesce(content_text, ''))";

    private const int EnumLength = 16;

    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(document => document.Title).HasColumnName("title").HasMaxLength(Document.MaxTitleLength);
        builder.Property(document => document.ReceivedAt).HasColumnName("received_at");
        builder.Property(document => document.State).HasColumnName("state").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(document => document.ContentText).HasColumnName("content_text");
        builder.Property(document => document.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(Document.MaxFailureReasonLength);

        // No foreign key to users: documents outlive a removed member (their private ones stay hidden),
        // and M1 documents have no owner until the household is set up.
        builder.Property(document => document.OwnerId).HasColumnName("owner_id");
        builder.Property(document => document.IsPrivate).HasColumnName("is_private");

        // Inputs (M4): where it came from; documents from before are uploads (default of the migration).
        builder.Property(document => document.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(document => document.NeedsOwnerCheck).HasColumnName("needs_owner_check");
        builder.ComplexProperty(document => document.MailOrigin, mail =>
        {
            mail.Property(origin => origin.SenderAddress).HasColumnName("mail_sender").HasMaxLength(MailOrigin.MaxSenderAddressLength);
            mail.Property(origin => origin.Subject).HasColumnName("mail_subject").HasMaxLength(MailOrigin.MaxSubjectLength);
            mail.Property(origin => origin.Note).HasColumnName("mail_note").HasMaxLength(MailOrigin.MaxNoteLength);
        });

        builder.Property<NpgsqlTsVector>(SearchVectorProperty)
            .HasColumnName("search_vector")
            .HasComputedColumnSql(SearchVectorSql, stored: true);
        builder.HasIndex(SearchVectorProperty).HasMethod("gin");

        // Trigrams find words that OCR misspelled.
        builder.HasIndex(document => document.ContentText).HasMethod("gin").HasOperators("gin_trgm_ops");

        // The list shows the newest documents first.
        builder.HasIndex(document => document.ReceivedAt);

        ConfigureEditing(builder);
        builder.OwnsMany(document => document.Files, ConfigureFiles);
        builder.Navigation(document => document.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // Trash, versions and split suggestion (M4b, ADR-0025). The named filter hides documents in the trash
    // from every EF query; the trash's own queries switch it off. Plain SQL uses TrashSql.
    private static void ConfigureEditing(EntityTypeBuilder<Document> builder)
    {
        builder.Property(document => document.DeletedAt).HasColumnName("deleted_at");
        builder.Property(document => document.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(TrashSql.FilterName, document => document.DeletedAt == null);
        builder.HasIndex(document => document.DeletedAt);

        builder.Property(document => document.VersionPolicy)
            .HasColumnName("version_policy")
            .HasConversion<string>()
            .HasMaxLength(EnumLength);
        builder.Property(document => document.EditedAt).HasColumnName("edited_at");
        builder.Property(document => document.SplitSuggestion)
            .HasColumnName("split_suggestion")
            .HasColumnType("jsonb")
            .HasConversion(new ValueConverter<SplitSuggestion?, string?>(
                suggestion => suggestion == null ? null : SplitSuggestionJson.Write(suggestion),
                json => json == null ? null : SplitSuggestionJson.Read(json)));
    }

    private static void ConfigureFiles(OwnedNavigationBuilder<Document, DocumentFile> file)
    {
        const string documentId = "DocumentId";

        file.ToTable("document_files");
        file.WithOwner().HasForeignKey(documentId);
        file.Property<Guid>(documentId).HasColumnName("document_id");
        file.HasKey(documentId, nameof(DocumentFile.Kind));
        file.Property(f => f.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(EnumLength);
        file.Property(f => f.FileName).HasColumnName("file_name").HasMaxLength(DocumentFile.MaxFileNameLength);
        file.Property(f => f.MediaType).HasColumnName("media_type").HasMaxLength(DocumentFile.MaxMediaTypeLength);
        file.Property(f => f.SizeBytes).HasColumnName("size_bytes");
        file.Property(f => f.Sha256).HasColumnName("sha256").HasMaxLength(DocumentFile.Sha256Length).IsFixedLength();

        // Finds an upload whose exact file is archived already (M3 duplicates); only originals count,
        // so queries must filter on kind = 'Original' to use it.
        file.HasIndex(f => f.Sha256).HasFilter("kind = 'Original'");
    }
}
