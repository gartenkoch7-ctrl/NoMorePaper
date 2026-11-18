using System.Net;

namespace ICloudSpike.Session;

/// <summary>Serializable copy of one cookie.</summary>
internal sealed record StoredCookie(
    string Name, string Value, string Domain, string Path, DateTimeOffset? Expires, bool Secure, bool HttpOnly)
{
    public static StoredCookie From(Cookie cookie) => new(
        cookie.Name,
        cookie.Value,
        cookie.Domain,
        cookie.Path,
        // DateTime.MinValue is how System.Net.Cookie marks a session cookie. Those are kept too:
        // Apple issues several of its sign-in cookies without an expiry date.
        cookie.Expires == DateTime.MinValue ? null : new DateTimeOffset(cookie.Expires.ToUniversalTime()),
        cookie.Secure,
        cookie.HttpOnly);

    public bool IsExpired(DateTimeOffset now) => Expires is { } expires && expires <= now;

    public Cookie ToCookie()
    {
        var cookie = new Cookie(Name, Value, Path, Domain) { Secure = Secure, HttpOnly = HttpOnly };
        if (Expires is { } expires)
        {
            cookie.Expires = expires.UtcDateTime;
        }

        return cookie;
    }
}
