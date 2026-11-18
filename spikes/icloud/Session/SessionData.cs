using System.Net;

namespace ICloudSpike.Session;

/// <summary>
/// Everything that must survive a restart so that no password and no 2FA code is needed again.
/// All of it is secret; it only ever leaves memory through <see cref="SessionStore"/>.
/// </summary>
internal sealed class SessionData
{
    /// <summary>Random id of this "browser"; Apple ties the sign-in session to it.</summary>
    public string ClientId { get; set; } = "auth-" + Guid.NewGuid().ToString("D");

    /// <summary>Header "scnt": Apple's per-sign-in continuation value.</summary>
    public string? Scnt { get; set; }

    /// <summary>Header "X-Apple-ID-Session-Id".</summary>
    public string? SessionId { get; set; }

    /// <summary>Header "X-Apple-Auth-Attributes".</summary>
    public string? AuthAttributes { get; set; }

    /// <summary>Header "X-Apple-Session-Token"; exchanged for cookies at accountLogin.</summary>
    public string? SessionToken { get; set; }

    /// <summary>Header "X-Apple-TwoSV-Trust-Token"; lets a later password sign-in skip 2FA.</summary>
    public string? TrustToken { get; set; }

    /// <summary>Header "X-Apple-ID-Account-Country".</summary>
    public string? AccountCountry { get; set; }

    /// <summary>When the user last confirmed a 2FA code; used to measure how long the trust lasts.</summary>
    public DateTimeOffset? TrustedAt { get; set; }

    public List<StoredCookie> Cookies { get; set; } = [];

    public void CaptureCookies(CookieContainer container) =>
        Cookies = [.. container.GetAllCookies().Where(cookie => !cookie.Expired).Select(StoredCookie.From)];

    public void RestoreCookies(CookieContainer container, DateTimeOffset now)
    {
        foreach (var stored in Cookies.Where(cookie => !cookie.IsExpired(now)))
        {
            try
            {
                container.Add(stored.ToCookie());
            }
            catch (CookieException)
            {
                // A cookie the jar no longer accepts is dropped; the token sign-in recreates it.
            }
        }
    }
}
