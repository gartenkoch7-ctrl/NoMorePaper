namespace NoMorePaper.Server.Tests.Hosting;

/// <summary>The sign-in cookie in answers of the test host (Development: named <c>nmp</c>, not Secure).</summary>
internal static class SessionCookie
{
    public const string Name = "nmp";

    /// <returns>The full <c>Set-Cookie</c> header of the sign-in cookie, or <c>null</c>.</returns>
    public static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(Name + "=", StringComparison.Ordinal))
            : null;

    /// <returns><c>name=value</c> to send back in a <c>Cookie</c> header, or <c>null</c>.</returns>
    public static string? From(HttpResponseMessage response) => SetCookieHeader(response)?.Split(';')[0];
}
