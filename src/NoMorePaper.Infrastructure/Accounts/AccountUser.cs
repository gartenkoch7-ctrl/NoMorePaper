using Microsoft.AspNetCore.Identity;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// An account as ASP.NET Identity stores it (table <c>users</c>). Identity stays in Infrastructure;
/// the rest of the application sees <see cref="Application.Accounts.Account"/>.
/// </summary>
public sealed class AccountUser : IdentityUser<Guid>
{
    /// <summary>Name shown to the household, e.g. "Anna"; the user name is for signing in.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Set for the one account that set up the household.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>SHA-256 of the emergency code (administrator only), never the code itself.</summary>
    public string? EmergencyCodeHash { get; set; }

    /// <summary>When a reset link from the administrator last set the password; cleared once the member saw the notice.</summary>
    public DateTimeOffset? PasswordResetByAdminAt { get; set; }
}
