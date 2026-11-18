namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Tells <see cref="SessionSignal"/> when any API call answers 401, so every page ends up on the
/// sign-in page without handling it itself. Calls where 401 is an expected answer (wrong
/// password, "who am I" while signed out) are left alone.
/// </summary>
internal sealed class UnauthorizedHandler(SessionSignal signal) : DelegatingHandler(new HttpClientHandler())
{
    private static readonly string[] _expected401 = ["api/auth/login", "api/auth/me", "api/emergency-reset"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !IsExpected(request))
        {
            signal.RaiseExpired();
        }

        return response;
    }

    private static bool IsExpected(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? string.Empty;
        return _expected401.Contains(path, StringComparer.Ordinal);
    }
}
