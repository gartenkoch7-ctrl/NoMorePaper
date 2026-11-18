using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// What the sign-in cookie carries: account id, security stamp and whether it is the administrator.
/// No names: those are read fresh from the store. The cookie itself is encrypted by Data Protection.
/// Pure logic.
/// </summary>
internal static class AccountClaims
{
    public const string SecurityStamp = "nmp:stamp";
    public const string Administrator = "nmp:admin";

    public static ClaimsPrincipal CreatePrincipal(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, account.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(SecurityStamp, account.SecurityStamp),
            new(Administrator, account.IsAdmin ? "true" : "false"),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <returns>The account id, or <c>null</c> for an anonymous or malformed principal.</returns>
    public static Guid? AccountId(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public static bool IsAdministrator(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirstValue(Administrator), "true", StringComparison.Ordinal);

    public static string? Stamp(ClaimsPrincipal principal) => principal.FindFirstValue(SecurityStamp);
}
