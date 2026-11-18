using ICloudSpike.Http;
using ICloudSpike.Session;

namespace ICloudSpike.Auth;

/// <summary>
/// Calls to Apple's sign-in service (idmsa.apple.com). Adds the headers that identify the iCloud
/// web app and carries Apple's sign-in state from one answer to the next request.
/// </summary>
internal sealed class AuthChannel
{
    public const string JsonAccept = "application/json";

    /// <summary>What the web app sends on the password steps.</summary>
    public const string ScriptAccept = "application/json, text/javascript";

    public const string HtmlAccept = "text/html";

    /// <summary>Public id of the iCloud web app at Apple's sign-in service; identical for every user.</summary>
    public const string WidgetKey = "d39ba9916b7251055b22c7f910e2ea796ee65e98b2ddecea8f5dde8d9d1a815d";

    private const string AuthUrl = "https://idmsa.apple.com/appleauth/auth";

    private readonly ICloudTransport _transport;
    private readonly SessionData _session;

    public AuthChannel(ICloudTransport transport, SessionData session)
    {
        _transport = transport;
        _session = session;
    }

    public string ClientId => _session.ClientId;

    /// <summary>Trust token of an earlier 2FA, if any.</summary>
    public string? TrustToken => _session.TrustToken;

    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path below ".../appleauth/auth", with query if needed; empty for the root.</param>
    /// <param name="body">JSON body, an empty string for "no content but Content-Length: 0", or null.</param>
    /// <param name="accept">Value of the Accept header; decides between JSON and HTML answers.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<ApiResponse> SendAsync(
        HttpMethod method, string path, string? body, string accept, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = accept,
            ["X-Apple-OAuth-Client-Id"] = WidgetKey,
            ["X-Apple-OAuth-Client-Type"] = "firstPartyAuth",
            ["X-Apple-OAuth-Redirect-URI"] = ICloudTransport.HomeUrl,
            ["X-Apple-OAuth-Require-Grant-Code"] = "true",
            ["X-Apple-OAuth-Response-Mode"] = "web_message",
            ["X-Apple-OAuth-Response-Type"] = "code",
            ["X-Apple-OAuth-State"] = _session.ClientId,
            ["X-Apple-Frame-Id"] = _session.ClientId,
            ["X-Apple-Widget-Key"] = WidgetKey,
        };
        AddIfKnown(headers, "scnt", _session.Scnt);
        AddIfKnown(headers, "X-Apple-ID-Session-Id", _session.SessionId);
        AddIfKnown(headers, "X-Apple-Auth-Attributes", _session.AuthAttributes);

        var response = await _transport.SendAsync(
            new ApiRequest(method, new Uri(AuthUrl + path)) { Body = body, Headers = headers }, cancellationToken);

        // Apple hands the sign-in state back in headers; each answer may replace earlier values.
        _session.Scnt = response.Header("scnt") ?? _session.Scnt;
        _session.SessionId = response.Header("X-Apple-ID-Session-Id") ?? _session.SessionId;
        _session.AuthAttributes = response.Header("X-Apple-Auth-Attributes") ?? _session.AuthAttributes;
        _session.SessionToken = response.Header("X-Apple-Session-Token") ?? _session.SessionToken;
        _session.TrustToken = response.Header("X-Apple-TwoSV-Trust-Token") ?? _session.TrustToken;
        _session.AccountCountry = response.Header("X-Apple-ID-Account-Country") ?? _session.AccountCountry;
        return response;
    }

    private static void AddIfKnown(Dictionary<string, string> headers, string name, string? value)
    {
        if (value is not null)
        {
            headers[name] = value;
        }
    }
}
