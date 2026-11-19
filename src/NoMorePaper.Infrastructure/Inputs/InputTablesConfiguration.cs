using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Infrastructure.Accounts;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>
/// Maps <c>input_connections</c>, <c>input_status</c> and <c>input_seen_items</c>. Every
/// <c>account_id</c> references <c>users</c> with <c>ON DELETE CASCADE</c>: a removed account's
/// session, state and seen items go with it. Names are spelled out because the stores use plain SQL.
/// </summary>
internal sealed class InputTablesConfiguration :
    IEntityTypeConfiguration<InputConnectionRow>,
    IEntityTypeConfiguration<InputStatusRow>,
    IEntityTypeConfiguration<SeenItemRow>
{
    private const int EnumLength = 16;

    /// <summary>Hex digits of a SHA-256.</summary>
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<InputConnectionRow> builder)
    {
        builder.ToTable("input_connections");
        builder.HasKey(row => new { row.Kind, row.AccountKey });
        builder.Property(row => row.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.AccountKey).HasColumnName("account_key");
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.Settings).HasColumnName("settings").HasColumnType("jsonb");
        builder.Property(row => row.Secret).HasColumnName("secret");
        builder.Property(row => row.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<InputStatusRow> builder)
    {
        builder.ToTable("input_status");
        builder.HasKey(row => new { row.Kind, row.AccountKey });
        builder.Property(row => row.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.AccountKey).HasColumnName("account_key");
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.LastRunAt).HasColumnName("last_run_at");
        builder.Property(row => row.LastSuccessAt).HasColumnName("last_success_at");
        builder.Property(row => row.Problem).HasColumnName("problem").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.NeedsAttention).HasColumnName("needs_attention");
        builder.Property(row => row.ReceivedTotal).HasColumnName("received_total");
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<SeenItemRow> builder)
    {
        builder.ToTable("input_seen_items");
        builder.HasKey(row => new { row.Kind, row.AccountKey, row.List, row.KeyHash });
        builder.Property(row => row.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.AccountKey).HasColumnName("account_key");
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.List).HasColumnName("list").HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(row => row.KeyHash).HasColumnName("key_hash").HasMaxLength(HashLength).IsFixedLength();
        builder.Property(row => row.Failures).HasColumnName("failures");
        builder.Property(row => row.SeenAt).HasColumnName("seen_at");
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
