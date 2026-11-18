namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Checks the page to open after signing in (<c>/anmelden?ziel=…</c>): only a path on this site,
/// never another one. The parameter comes from a link anyone can craft, so it is distrusted.
/// </summary>
public static class SignInTarget
{
    /// <summary>Where to go when the target is missing or unsafe.</summary>
    public const string Fallback = "/";

    /// <returns>
    /// <paramref name="target"/> when it is a path starting with a single <c>/</c>, has no control
    /// characters, whitespace or backslash, and resolves against <paramref name="baseUri"/> to the
    /// same scheme, host and port; otherwise <see cref="Fallback"/>.
    /// </returns>
    public static string Safe(string? target, string baseUri)
    {
        if (string.IsNullOrEmpty(target)
            || target[0] != '/'
            || (target.Length > 1 && target[1] == '/')
            || target.Any(IsRefused)
            || !Uri.TryCreate(baseUri, UriKind.Absolute, out var site)
            || !Uri.TryCreate(site, target, out var resolved))
        {
            return Fallback;
        }

        return IsSameOrigin(site, resolved) ? target : Fallback;
    }

    // Browsers drop tabs and line breaks inside a URL and read a backslash as a slash, so
    // "/\t/evil" or "/\evil" would become "//evil", a link to another site.
    private static bool IsRefused(char c) => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\';

    private static bool IsSameOrigin(Uri site, Uri resolved) =>
        string.Equals(site.Scheme, resolved.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(site.Host, resolved.Host, StringComparison.OrdinalIgnoreCase)
        && site.Port == resolved.Port;
}
