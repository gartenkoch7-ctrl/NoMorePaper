using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ICloudSpike.Auth;

namespace ICloudSpike.SelfTest;

/// <summary>
/// Simulates idmsa.apple.com as the public clients describe it, including the server side of SRP,
/// so a wrong proof really is rejected. It models the documented behaviour, not Apple's real one.
/// </summary>
internal sealed class FakeAuthHost
{
    private const string Root = "/appleauth/auth";
    private const int Iterations = 1000;

    private static readonly SrpGroup Group = SrpGroup.Rfc5054Bits2048;
    private static readonly HashAlgorithmName Hash = HashAlgorithmName.SHA256;
    private static readonly byte[] Salt = Convert.FromHexString("A1B2C3D4E5F60718293A4B5C6D7E8F90");

    private readonly FakeAppleAccount _account;
    private BigInteger _clientPublic;
    private BigInteger _serverPrivate;
    private BigInteger _serverPublic;
    private BigInteger _verifier;

    public FakeAuthHost(FakeAppleAccount account) => _account = account;

    public HttpResponseMessage Handle(HttpRequestMessage request, string body) =>
        (request.Method.Method, request.RequestUri!.AbsolutePath) switch
        {
            ("GET", Root + "/authorize/signin") => FakeResponses.Html(
                200, "<html></html>", ("scnt", "SCNT-0"), ("X-Apple-Auth-Attributes", "ATTR-" + FakeAppleAccount.SecretMarker)),
            ("POST", Root + "/signin/init") => Init(body),
            ("POST", Root + "/signin/complete") => Complete(request, body),
            _ when !HasSignInSession(request) => FakeResponses.Empty(401),
            ("GET", Root) => Options(request),
            ("PUT", Root + "/verify/trusteddevice/securitycode") => RequestPush(request),
            ("POST", Root + "/verify/trusteddevice/securitycode") => VerifyDeviceCode(body),
            ("PUT", Root + "/verify/phone") => RequestSms(body),
            ("POST", Root + "/verify/phone/securitycode") => VerifySmsCode(body),
            ("GET", Root + "/2sv/trust") => Trust(),
            _ => FakeResponses.Empty(404),
        };

    private static bool HasSignInSession(HttpRequestMessage request) =>
        request.Headers.Contains("scnt") && request.Headers.Contains("X-Apple-ID-Session-Id");

    private HttpResponseMessage Init(string body)
    {
        if (_account.InitStatus is { } status)
        {
            return FakeResponses.Html(status, "<html>Service Temporarily Unavailable</html>");
        }

        using var json = JsonDocument.Parse(body);
        _clientPublic = SrpMath.ToInteger(Convert.FromBase64String(json.RootElement.GetProperty("a").GetString()!));
        var key = ApplePasswordKey.Derive(
            Encoding.UTF8.GetBytes(FakeAppleAccount.Password), _account.Protocol, Salt, Iterations);
        _verifier = SrpMath.ComputeVerifier(Group, SrpMath.ComputePrivateExponent(Hash, Salt, string.Empty, key));
        _serverPrivate = SrpMath.ToInteger(RandomNumberGenerator.GetBytes(32));
        var multiplier = SrpMath.ComputeMultiplier(Group, Hash);
        _serverPublic = ((multiplier * _verifier) + BigInteger.ModPow(Group.G, _serverPrivate, Group.N)) % Group.N;

        var answer = new JsonObject
        {
            ["iteration"] = Iterations,
            ["salt"] = Convert.ToBase64String(Salt),
            ["protocol"] = _account.Protocol,
            ["b"] = Convert.ToBase64String(SrpMath.ToMinimalBytes(_serverPublic)),
            ["c"] = "continuation-" + FakeAppleAccount.SecretMarker,
        };
        return FakeResponses.Json(200, answer.ToJsonString());
    }

    private HttpResponseMessage Complete(HttpRequestMessage request, string body)
    {
        if (request.RequestUri!.Query != "?isRememberMeEnabled=true")
        {
            return FakeResponses.Empty(400);
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var scrambler = SrpMath.ComputeScrambler(Group, Hash, _clientPublic, _serverPublic);
        var secret = SrpMath.ComputeServerSecret(Group, _verifier, _serverPrivate, scrambler, _clientPublic);
        var sessionKey = SrpMath.ComputeSessionKey(Hash, secret);
        var expectedProof = SrpMath.ComputeClientProof(
            Group, Hash, FakeAppleAccount.AccountName, Salt, _clientPublic, _serverPublic, sessionKey);
        var expectedServerProof = SrpMath.ComputeServerProof(Hash, _clientPublic, expectedProof, sessionKey);
        var proofMatches = root.GetProperty("accountName").GetString() == FakeAppleAccount.AccountName
            && Convert.FromBase64String(root.GetProperty("m1").GetString()!).AsSpan().SequenceEqual(expectedProof)
            && Convert.FromBase64String(root.GetProperty("m2").GetString()!).AsSpan().SequenceEqual(expectedServerProof)
            && root.GetProperty("c").GetString() == "continuation-" + FakeAppleAccount.SecretMarker;
        if (!proofMatches)
        {
            return FakeResponses.Json(
                401, """{"serviceErrors":[{"code":"-20101","message":"Your Apple Account or password was incorrect."}]}""");
        }

        if (_account.Locked)
        {
            return FakeResponses.Json(
                403, """{"serviceErrors":[{"code":"-20209","message":"This Apple Account has been locked."}]}""");
        }

        var trusted = root.GetProperty("trustTokens").EnumerateArray()
            .Any(token => token.GetString() == FakeAppleAccount.TrustToken);
        return trusted
            ? FakeResponses.Json(200, "{}", SessionHeaders(_account.TrustedSessionToken))
            : FakeResponses.Json(409, """{"authType":"hsa2"}""", SessionHeaders(FakeAppleAccount.UntrustedSessionToken));
    }

    private static (string Name, string Value)[] SessionHeaders(string sessionToken) =>
    [
        ("X-Apple-Session-Token", sessionToken),
        ("X-Apple-ID-Session-Id", "SESSIONID-" + FakeAppleAccount.SecretMarker),
        ("X-Apple-ID-Account-Country", "DEU"),
        ("scnt", "SCNT-" + FakeAppleAccount.SecretMarker),
    ];

    /// <summary>
    /// The HTML variant carries the phone numbers at the place Apple moved them to in 2026; the
    /// JSON variant uses the older top-level field.
    /// </summary>
    private static HttpResponseMessage Options(HttpRequestMessage request)
    {
        const string phones =
            """[{"id":1,"obfuscatedNumber":"•••• •••••11"},{"id":2,"obfuscatedNumber":"•••• •••••22"}]""";
        var wantsJson = request.Headers.Accept.Any(accept => accept.MediaType == "application/json");
        const string json = """{"trustedPhoneNumbers":PHONES}""";
        const string html =
            """
            <html><head><script type="application/json" class="boot_args">
            {"direct":{"hasTrustedDevices":true,"twoSV":{"phoneNumberVerification":{"trustedPhoneNumbers":[]},
            "bridgeInitiateData":{"phoneNumberVerification":{"trustedPhoneNumbers":PHONES}}}}}
            </script></head><body></body></html>
            """;
        return wantsJson
            ? FakeResponses.Json(200, json.Replace("PHONES", phones, StringComparison.Ordinal))
            : FakeResponses.Html(409, html.Replace("PHONES", phones, StringComparison.Ordinal));
    }

    private HttpResponseMessage RequestPush(HttpRequestMessage request)
    {
        // A browser sends "Content-Length: 0" on this PUT; without a content object .NET sends none.
        if (request.Content?.Headers.ContentLength != 0)
        {
            return FakeResponses.Empty(411);
        }

        _account.PushRequests++;
        return FakeResponses.Json(202, "{}");
    }

    private HttpResponseMessage VerifyDeviceCode(string body)
    {
        using var json = JsonDocument.Parse(body);
        if (json.RootElement.GetProperty("securityCode").GetProperty("code").GetString() != FakeAppleAccount.DeviceCode)
        {
            return FakeResponses.Json(
                400, """{"service_errors":[{"code":"-21669","message":"Incorrect verification code."}],"hasError":true}""");
        }

        _account.CodeVerified = true;
        return FakeResponses.Empty(204);
    }

    private HttpResponseMessage RequestSms(string body)
    {
        using var json = JsonDocument.Parse(body);
        var valid = json.RootElement.GetProperty("mode").GetString() == "sms"
            && json.RootElement.GetProperty("phoneNumber").GetProperty("id").GetInt32() == 2;
        if (!valid)
        {
            return FakeResponses.Empty(400);
        }

        _account.SmsRequests++;
        return FakeResponses.Json(200, "{}");
    }

    private HttpResponseMessage VerifySmsCode(string body)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var valid = root.GetProperty("securityCode").GetProperty("code").GetString() == FakeAppleAccount.SmsCode
            && root.GetProperty("phoneNumber").GetProperty("id").GetInt32() == 2
            && root.GetProperty("mode").GetString() == "sms";
        if (!valid)
        {
            return FakeResponses.Json(400, """{"service_errors":[{"code":"-21669","message":"Incorrect verification code."}]}""");
        }

        // Publicly reported quirk: an accepted SMS code can come back as 409 with "valid": true.
        _account.CodeVerified = true;
        return FakeResponses.Json(409, """{"securityCode":{"valid":true}}""");
    }

    private HttpResponseMessage Trust() => _account.CodeVerified
        ? FakeResponses.Empty(
            204,
            ("X-Apple-TwoSV-Trust-Token", FakeAppleAccount.TrustToken),
            ("X-Apple-Session-Token", _account.TrustedSessionToken))
        : FakeResponses.Empty(401);
}
