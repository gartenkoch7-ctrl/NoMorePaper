using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Accounts;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>
/// Maps <c>account_preferences</c> and <c>account_mail_senders</c>. Both go with their account
/// (<c>ON DELETE CASCADE</c>): a removed member's addresses are free again (ADR-0024).
/// </summary>
internal sealed class AccountPreferencesConfiguration :
    IEntityTypeConfiguration<AccountPreferencesRow>,
    IEntityTypeConfiguration<MailSenderRow>
{
    private const int EnumLength = 16;

    public void Configure(EntityTypeBuilder<AccountPreferencesRow> builder)
    {
        builder.ToTable("account_preferences");
        builder.HasKey(row => row.AccountId);
        builder.Property(row => row.AccountId).HasColumnName("account_id").ValueGeneratedNever();
        builder.Property(row => row.NewDocumentsPrivate).HasColumnName("new_documents_private");

        // Written by plain SQL only; rows from before M4b get the default of the migration.
        builder.Property(row => row.AfterEditing)
            .HasColumnName("after_editing")
            .HasConversion<string>()
            .HasMaxLength(EnumLength)
            .HasDefaultValue(VersionPolicy.KeepBoth)
            .HasSentinel((VersionPolicy)(-1));
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Cascade);
    }

    // The address is the key: one address belongs to at most one account, enforced by the database.
    public void Configure(EntityTypeBuilder<MailSenderRow> builder)
    {
        builder.ToTable("account_mail_senders");
        builder.HasKey(row => row.Address);
        builder.Property(row => row.Address).HasColumnName("address").HasMaxLength(AccountPreferences.MaxAddressLength);
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.HasIndex(row => row.AccountId);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
