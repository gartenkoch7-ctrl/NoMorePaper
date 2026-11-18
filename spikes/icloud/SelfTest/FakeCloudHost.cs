using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ICloudSpike.SelfTest;

/// <summary>Simulates setup.icloud.com, the Drive services and the content server.</summary>
internal sealed class FakeCloudHost
{
    public const string DriveHost = "p01-drivews.icloud.com";
    public const string DocumentsHost = "p01-docws.icloud.com";
    public const string ContentHost = "cvws.icloud-content.com";
    public const string SubFolderId = "FOLDER::com.apple.CloudDocs::SUB-1";

    // More cookies than the .NET default of 20 per domain, and one larger than the default 4 KB,
    // to prove the cookie jar keeps what Apple really sends.
    private const int ExtraCookies = 25;
    private const int LargeCookieLength = 6000;

    private readonly FakeAppleAccount _account;

    public FakeCloudHost(FakeAppleAccount account) => _account = account;

    public static byte[] FileContent { get; } = Encoding.UTF8.GetBytes("%PDF-1.7 self-test document, not a real file");

    /// <summary>Set if the client sent cookies or an Origin header to the content server.</summary>
    public bool ContentRequestLeakedHeaders { get; private set; }

    public HttpResponseMessage Handle(HttpRequestMessage request, string body)
    {
        var uri = request.RequestUri!;
        return (uri.Host, request.Method.Method, uri.AbsolutePath) switch
        {
            ("setup.icloud.com", "POST", "/setup/ws/1/accountLogin") => AccountLogin(body),
            ("setup.icloud.com", "POST", "/setup/ws/1/validate") => Validate(request, body),
            (DriveHost, "POST", "/retrieveItemDetailsInFolders") => ListFolder(request, body),
            (DocumentsHost, "GET", "/ws/com.apple.CloudDocs/download/by_id") => ResolveDownload(request),
            (ContentHost, "GET", _) => Content(request),
            _ => FakeResponses.Empty(404),
        };
    }

    private HttpResponseMessage AccountLogin(string body)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var token = root.GetProperty("dsWebAuthToken").GetString();
        if (!root.GetProperty("extended_login").GetBoolean() || root.GetProperty("accountCountryCode").GetString() != "DEU")
        {
            return FakeResponses.Empty(400);
        }

        if (token == FakeAppleAccount.UntrustedSessionToken)
        {
            return FakeResponses.Json(200, AccountJson(trusted: false));
        }

        if (token != _account.TrustedSessionToken || root.GetProperty("trustToken").GetString() != FakeAppleAccount.TrustToken)
        {
            return FakeResponses.Json(421, """{"error":"Invalid authentication token"}""");
        }

        return FakeResponses.Json(200, AccountJson(trusted: true), Cookies());
    }

    private HttpResponseMessage Validate(HttpRequestMessage request, string body) =>
        body == "null" && HasSessionCookies(request)
            ? FakeResponses.Json(200, AccountJson(trusted: true))
            : FakeResponses.Json(421, """{"error":"Missing X-APPLE-WEBAUTH-TOKEN cookie"}""");

    private (string Name, string Value)[] Cookies()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(30).ToString("R", CultureInfo.InvariantCulture);
        var cookies = new List<(string Name, string Value)>
        {
            ("Set-Cookie", $"X-APPLE-WEBAUTH-TOKEN=\"v=2:t={_account.WebAuthCookie}\"; Domain=.icloud.com; Path=/; Expires={expires}; Secure; HttpOnly"),
            ("Set-Cookie", $"X-APPLE-WEBAUTH-PCS-Documents=\"{new string('P', LargeCookieLength)}\"; Domain=.icloud.com; Path=/; Secure; HttpOnly"),
            ("Set-Cookie", $"X-APPLE-DS-WEB-SESSION-TOKEN=\"HOSTONLY-{FakeAppleAccount.SecretMarker}\"; Path=/; Secure"),
        };
        for (var index = 0; index < ExtraCookies; index++)
        {
            cookies.Add(("Set-Cookie", string.Create(
                CultureInfo.InvariantCulture, $"X-APPLE-EXTRA-{index}=value{index}; Domain=.icloud.com; Path=/; Secure")));
        }

        return [.. cookies];
    }

    private bool HasSessionCookies(HttpRequestMessage request)
    {
        var header = request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : string.Empty;
        return header.Contains($"X-APPLE-WEBAUTH-TOKEN=\"v=2:t={_account.WebAuthCookie}\"", StringComparison.Ordinal)
            && header.Contains(new string('P', LargeCookieLength), StringComparison.Ordinal)
            && header.Contains("X-APPLE-EXTRA-0=value0", StringComparison.Ordinal)
            && header.Contains(
                string.Create(CultureInfo.InvariantCulture, $"X-APPLE-EXTRA-{ExtraCookies - 1}="), StringComparison.Ordinal);
    }

    private string AccountJson(bool trusted)
    {
        var services = _account.WebAccessAllowed
            ? $$"""
              "drivews":{"url":"https://{{DriveHost}}:443","status":"active"},
              "docws":{"url":"https://{{DocumentsHost}}:443","status":"active"}
              """
            : string.Empty;
        return $$"""
            {
              "dsInfo":{"dsid":"{{FakeAppleAccount.Dsid}}","hsaVersion":2,
                        "isWebAccessAllowed":{{(_account.WebAccessAllowed ? "true" : "false")}} },
              "hsaChallengeRequired":{{(trusted ? "false" : "true")}},
              "hsaTrustedBrowser":{{(trusted ? "true" : "false")}},
              "iCDPEnabled":{{(_account.WebAccessAllowed ? "false" : "true")}},
              "webservices":{ {{services}} }
            }
            """;
    }

    private HttpResponseMessage ListFolder(HttpRequestMessage request, string body)
    {
        if (_account.DriveStatus is { } status)
        {
            return FakeResponses.Json(status, """{"error":"Invalid global session"}""");
        }

        var query = request.RequestUri!.Query;
        if (!HasSessionCookies(request)
            || !query.Contains("dsid=" + FakeAppleAccount.Dsid, StringComparison.Ordinal)
            || !query.Contains("clientId=auth-", StringComparison.Ordinal))
        {
            return FakeResponses.Empty(401);
        }

        using var json = JsonDocument.Parse(body);
        var folderId = json.RootElement[0].GetProperty("drivewsid").GetString();
        var length = FileContent.Length.ToString(CultureInfo.InvariantCulture);
        return folderId switch
        {
            "FOLDER::com.apple.CloudDocs::root" => FakeResponses.Json(200, $$"""
                [{"drivewsid":"FOLDER::com.apple.CloudDocs::root","type":"FOLDER","numberOfItems":3,"items":[
                  {"drivewsid":"{{SubFolderId}}","docwsid":"SUB-1","zone":"com.apple.CloudDocs","name":"Scans","type":"FOLDER"},
                  {"drivewsid":"FILE::com.apple.CloudDocs::DOC-1","docwsid":"DOC-1","zone":"com.apple.CloudDocs",
                   "name":"Rechnung","extension":"pdf","type":"FILE","size":{{length}} },
                  {"drivewsid":"FILE::com.apple.CloudDocs::DOC-2","docwsid":"DOC-2","zone":"com.apple.CloudDocs",
                   "name":"leer","extension":"txt","type":"FILE","size":0}
                ]}]
                """),
            SubFolderId => FakeResponses.Json(200, $$"""
                [{"drivewsid":"{{SubFolderId}}","type":"FOLDER","items":[
                  {"drivewsid":"FILE::com.apple.CloudDocs::DOC-3","docwsid":"DOC-1","zone":"com.apple.CloudDocs",
                   "name":"..\\..\\brief:2026","extension":"pdf","type":"FILE","size":"{{length}}"}
                ]}]
                """),
            _ => FakeResponses.Json(200, """[{"drivewsid":"unknown","status":"ID_INVALID"}]"""),
        };
    }

    private HttpResponseMessage ResolveDownload(HttpRequestMessage request)
    {
        var query = request.RequestUri!.Query;
        if (!HasSessionCookies(request) || !query.Contains("document_id=DOC-1", StringComparison.Ordinal))
        {
            return FakeResponses.Empty(401);
        }

        return FakeResponses.Json(200, $$"""
            {"document_id":"DOC-1","data_token":{"url":"https://{{ContentHost}}/B/signed-{{FakeAppleAccount.SecretMarker}}/first?o=1"} }
            """);
    }

    private HttpResponseMessage Content(HttpRequestMessage request)
    {
        if (request.Headers.Contains("Cookie") || request.Headers.Contains("Origin"))
        {
            ContentRequestLeakedHeaders = true;
        }

        // One redirect, as content servers may hand the client on to another node.
        return request.RequestUri!.AbsolutePath.EndsWith("/first", StringComparison.Ordinal)
            ? FakeResponses.Redirect(new Uri($"https://{ContentHost}/B/signed-{FakeAppleAccount.SecretMarker}/second"))
            : FakeResponses.Bytes(FileContent);
    }
}
