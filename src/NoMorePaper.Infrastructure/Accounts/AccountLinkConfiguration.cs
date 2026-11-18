using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>Maps invitations and reset links; both go away with their account.</summary>
internal sealed class AccountLinkConfiguration : IEntityTypeConfiguration<Invitation>, IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(invitation => invitation.TokenHash).HasColumnName("token_hash").HasMaxLength(SecretHash.Length).IsFixedLength();
        builder.Property(invitation => invitation.CreatedBy).HasColumnName("created_by");
        builder.Property(invitation => invitation.CreatedAt).HasColumnName("created_at");
        builder.Property(invitation => invitation.ExpiresAt).HasColumnName("expires_at");
        builder.Property(invitation => invitation.UsedAt).HasColumnName("used_at");
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(invitation => invitation.CreatedBy).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(token => token.TokenHash);
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(SecretHash.Length).IsFixedLength();
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
