using System.Net;
using System.Text.Json;
using ICloudSpike.Auth;
using ICloudSpike.Drive;
using ICloudSpike.Http;
using ICloudSpike.Session;

namespace ICloudSpike.SelfTest;

/// <summary>Request/response models and the mapping of Apple's error answers.</summary>
internal static class ParsingSelfTests
{
    public static IEnumerable<(string Name, Func<Task> Run)> All =>
    [
        ("Modelle: Telefonnummern aus JSON und aus HTML (alter und neuer Ort)", Sync(TrustedPhones)),
        ("Modelle: Apples Fehlerformate", Sync(AppleErrors)),
        ("Modelle: Statuscodes 421, 450, 429, 503 und Sperrcode", Sync(StatusClassification)),
        ("Modelle: Kontostatus inklusive Erweitertem Datenschutz", Sync(AccountStates)),
        ("Modelle: Drive-Einträge und Anmelde-Antwort mit Lücken", Sync(ItemsAndChallenge)),
        ("Modelle: Cookies speichern und wiederherstellen, Adressen bauen", Sync(CookiesAndUris)),
    ];

    private static Func<Task> Sync(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    private static ApiResponse Response(int status, string body) => new(status, new Dictionary<string, string>(), body);

    private static void TrustedPhones()
    {
        var fromJson = TrustedPhoneParser.Parse(
            """{"trustedPhoneNumbers":[{"id":1,"obfuscatedNumber":"•• 11"},{"id":"2","numberWithDialCode":"+49 •• 22"}]}""");
        Check.Equal(2, fromJson.Count, "phones in JSON");
        Check.Equal(2, fromJson[1].Id, "id given as text");

        var fromHtml = TrustedPhoneParser.Parse(
            """
            <html><script type="application/json" class="boot_args">
            {"direct":{"twoSV":{"phoneNumberVerification":{"trustedPhoneNumbers":[{"id":7,"obfuscatedNumber":"•• 77"}]},
            "bridgeInitiateData":{"phoneNumberVerification":{"trustedPhoneNumbers":[{"id":7,"obfuscatedNumber":"•• 77"}]}}}}}
            </script></html>
            """);
        Check.Equal(1, fromHtml.Count, "the same number listed twice counts once");
        Check.Equal(7, fromHtml[0].Id, "phone id from HTML");

        Check.Equal(0, TrustedPhoneParser.Parse("<html>no data</html>").Count, "page without data");
        Check.Equal(0, TrustedPhoneParser.Parse("{ broken").Count, "broken JSON");
        Check.Equal(0, TrustedPhoneParser.Parse(string.Empty).Count, "empty body");
    }

    private static void AppleErrors()
    {
        var signIn = AppleError.From(Response(401, """{"serviceErrors":[{"code":"-20101","message":"wrong"}]}"""));
        Check.Equal<string?>(AppleError.WrongCredentials, signIn.Code, "serviceErrors code");
        var verify = AppleError.From(Response(400, """{"service_errors":[{"code":-21669,"message":"bad code"}]}"""));
        Check.Equal<string?>(AppleError.WrongSecurityCode, verify.Code, "numeric service_errors code");
        var service = AppleError.From(Response(421, """{"reason":"Missing cookie","errorCode":7}"""));
        Check.Equal<string?>("7", service.Code, "errorCode");
        Check.Equal<string?>("Missing cookie", service.Message, "reason");
        var html = AppleError.From(Response(503, "<html>Service Temporarily Unavailable</html>"));
        Check.Equal<string?>(null, html.Code, "HTML error page");
        var longText = AppleError.From(Response(500, $$"""{"error":"{{new string('x', 500)}}"}"""));
        Check.That(longText.Message!.Length < 200, "long messages are shortened");
    }

    private static void StatusClassification()
    {
        var none = new AppleError(null, null);
        Check.Equal(ICloudFailure.SessionExpired, ResponseErrors.Classify(421, none), "421");
        Check.Equal(ICloudFailure.SessionExpired, ResponseErrors.Classify(450, none), "450");
        Check.Equal(ICloudFailure.RateLimited, ResponseErrors.Classify(429, none), "429");
        Check.Equal(ICloudFailure.RateLimited, ResponseErrors.Classify(503, none), "503");
        Check.Equal(ICloudFailure.Unexpected, ResponseErrors.Classify(500, none), "500");
        Check.Equal(
            ICloudFailure.AccountLocked,
            ResponseErrors.Classify(403, new AppleError(AppleError.AccountLocked, null)),
            "locked account");

        var exception = ResponseErrors.Create("step", Response(409, """{"errorCode":"X1","reason":"conflict"}"""));
        Check.Equal(ICloudFailure.Unexpected, exception.Failure, "409 outside the sign-in");
        Check.Equal<int?>(409, exception.StatusCode, "status code kept");
        Check.That(exception.Message.Contains("X1", StringComparison.Ordinal), "Apple code in the message");
    }

    private static void AccountStates()
    {
        using var full = JsonDocument.Parse(
            """
            {"dsInfo":{"dsid":123456789,"hsaVersion":2,"isWebAccessAllowed":false},
             "hsaChallengeRequired":false,"hsaTrustedBrowser":true,"iCDPEnabled":true,
             "webservices":{"drivews":{"url":"https://p1-drivews.icloud.com:443"},"docws":{"url":"https://p1-docws.icloud.com:443"}}}
            """);
        var state = AccountState.Parse(full.RootElement);
        Check.Equal<string?>("123456789", state.Dsid, "numeric dsid");
        Check.That(!state.RequiresTwoFactor, "trusted session needs no code");
        Check.Equal<bool?>(false, state.WebAccessAllowed, "web access flag");
        Check.Equal<bool?>(true, state.AdvancedProtection, "Advanced Data Protection flag");
        Check.Equal<string?>("https://p1-docws.icloud.com:443", state.DocumentsUrl, "docws address");

        using var untrusted = JsonDocument.Parse("""{"dsInfo":{"hsaVersion":2},"hsaTrustedBrowser":false}""");
        Check.That(AccountState.Parse(untrusted.RootElement).RequiresTwoFactor, "untrusted session needs a code");

        using var empty = JsonDocument.Parse("{}");
        var minimal = AccountState.Parse(empty.RootElement);
        Check.That(!minimal.RequiresTwoFactor, "account without second factor");
        Check.Equal<bool?>(null, minimal.WebAccessAllowed, "missing flag stays unknown");
        Check.Equal<string?>(null, minimal.DriveUrl, "missing service");
    }

    private static void ItemsAndChallenge()
    {
        using var item = JsonDocument.Parse(
            """{"drivewsid":"FILE::z::1","docwsid":"1","name":"a","extension":"pdf","type":"FILE","size":"12"}""");
        var parsed = DriveItem.Parse(item.RootElement);
        Check.Equal<string?>("a.pdf", parsed?.FileName, "file name");
        Check.Equal<long?>(12, parsed?.Size, "size given as text");
        Check.Equal<string?>(DriveItem.DefaultZone, parsed?.Zone, "default zone");
        using var nameless = JsonDocument.Parse("""{"drivewsid":"FILE::z::2"}""");
        Check.That(DriveItem.Parse(nameless.RootElement) is null, "entry without name is skipped");

        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => SrpChallenge.Parse(Response(200, """{"salt":"AA==","b":"AA==","iteration":1000}""")),
            "sign-in answer without fields");
        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => SrpChallenge.Parse(Response(200, """{"salt":"%%","b":"AA==","c":"c","iteration":1000,"protocol":"s2k"}""")),
            "sign-in answer with broken Base64");
        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => SrpChallenge.Parse(Response(200, "<html></html>")),
            "sign-in answer that is no JSON");
        var challenge = SrpChallenge.Parse(
            Response(200, """{"salt":"AQI=","b":"AwQ=","c":"c1","iteration":"20000","protocol":"s2k_fo"}"""));
        Check.Equal(20000, challenge.Iterations, "iteration count given as text");
    }

    private static void CookiesAndUris()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new SessionData
        {
            Cookies =
            [
                new StoredCookie("keep", "\"v=1:t=abc\"", ".icloud.com", "/", now.AddDays(1), Secure: true, HttpOnly: true),
                new StoredCookie("session", "s", "setup.icloud.com", "/", Expires: null, Secure: true, HttpOnly: false),
                new StoredCookie("old", "x", ".icloud.com", "/", now.AddDays(-1), Secure: true, HttpOnly: false),
            ],
        };
        var jar = new CookieContainer();
        session.RestoreCookies(jar, now);
        var header = jar.GetCookieHeader(new Uri("https://setup.icloud.com/setup/ws/1/validate"));
        Check.That(header.Contains("keep=\"v=1:t=abc\"", StringComparison.Ordinal), "quoted cookie value changed");
        Check.That(header.Contains("session=s", StringComparison.Ordinal), "session cookie lost");
        Check.That(!header.Contains("old=", StringComparison.Ordinal), "expired cookie restored");
        Check.That(
            jar.GetCookieHeader(new Uri("https://p1-drivews.icloud.com/x")).Contains("keep=", StringComparison.Ordinal),
            "domain cookie not sent to a service host");
        Check.Equal(
            string.Empty, jar.GetCookieHeader(new Uri("https://cvws.icloud-content.com/x")), "cookies for a foreign host");

        session.CaptureCookies(jar);
        Check.Equal(2, session.Cookies.Count, "captured cookies");

        var uri = ApiRequest.BuildUri("https://example.invalid/a", new Dictionary<string, string> { ["k"] = "a b&c" });
        Check.Equal("https://example.invalid/a?k=a%20b%26c", uri.AbsoluteUri, "escaped query");
    }
}
