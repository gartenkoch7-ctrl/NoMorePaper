using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// Gives the Identity tables the snake_case names of the rest of the schema (<c>users</c>,
/// <c>user_claims</c>, …). Runs after Identity's own model setup and only renames; keys and indexes
/// stay as Identity defines them. Names are spelled out because the store uses plain SQL on them.
/// </summary>
internal sealed class AccountUserConfiguration :
    IEntityTypeConfiguration<AccountUser>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<AccountUser> builder)
    {
        builder.ToTable("users");
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.UserName).HasColumnName("user_name");
        builder.Property(user => user.NormalizedUserName).HasColumnName("normalized_user_name");
        builder.Property(user => user.Email).HasColumnName("email");
        builder.Property(user => user.NormalizedEmail).HasColumnName("normalized_email");
        builder.Property(user => user.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash");
        builder.Property(user => user.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(user => user.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(user => user.PhoneNumber).HasColumnName("phone_number");
        builder.Property(user => user.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(user => user.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(user => user.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(user => user.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(AccountRules.MaxDisplayNameLength);
        builder.Property(user => user.IsAdmin).HasColumnName("is_admin");
        builder.Property(user => user.EmergencyCodeHash)
            .HasColumnName("emergency_code_hash")
            .HasMaxLength(SecretHash.Length)
            .IsFixedLength();
    }

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims");
        builder.Property(claim => claim.Id).HasColumnName("id");
        builder.Property(claim => claim.UserId).HasColumnName("user_id");
        builder.Property(claim => claim.ClaimType).HasColumnName("claim_type");
        builder.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
    }

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins");
        builder.Property(login => login.LoginProvider).HasColumnName("login_provider");
        builder.Property(login => login.ProviderKey).HasColumnName("provider_key");
        builder.Property(login => login.ProviderDisplayName).HasColumnName("provider_display_name");
        builder.Property(login => login.UserId).HasColumnName("user_id");
    }

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.LoginProvider).HasColumnName("login_provider");
        builder.Property(token => token.Name).HasColumnName("name");
        builder.Property(token => token.Value).HasColumnName("value");
    }
}
