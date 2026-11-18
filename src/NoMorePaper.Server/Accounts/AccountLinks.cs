using Microsoft.AspNetCore.Http.Extensions;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// Links handed out for invitations and resets, pointing at the app's pages (Web.Client), and the
/// paths that carry such a token, so telemetry can leave them out. Pure logic.
/// </summary>
internal static class AccountLinks
{
    public const string InvitationPage = "/einladung";
    public const string ResetPage = "/passwort";

    private static readonly PathString[] s_tokenPaths =
        [InvitationPage, ResetPage, "/api/invitations", "/api/password-reset"];

    /// <summary>Absolute link with the scheme and host the browser used (forwarded by <c>tailscale serve</c>).</summary>
    public static string Build(HttpRequest request, string page, string token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, new PathString(page).Add("/" + token));
    }

    /// <summary>Whether the path contains a link token: anything below the pages and API routes of links.</summary>
    public static bool CarriesToken(PathString path)
    {
        foreach (var prefix in s_tokenPaths)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase, out var rest) && rest.HasValue && rest.Value != "/")
            {
                return true;
            }
        }

        return false;
    }
}
