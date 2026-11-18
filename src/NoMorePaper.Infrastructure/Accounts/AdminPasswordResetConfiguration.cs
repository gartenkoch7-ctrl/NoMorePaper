using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>Maps the record of resets through the administrator's links; it goes away with its account.</summary>
internal sealed class AdminPasswordResetConfiguration : IEntityTypeConfiguration<AdminPasswordReset>
{
    public void Configure(EntityTypeBuilder<AdminPasswordReset> builder)
    {
        builder.ToTable("admin_password_resets");
        builder.HasKey(reset => reset.Id);
        builder.Property(reset => reset.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(reset => reset.UserId).HasColumnName("user_id");
        builder.Property(reset => reset.ResetAt).HasColumnName("reset_at");
        builder.Property(reset => reset.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.HasIndex(reset => reset.UserId);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(reset => reset.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
