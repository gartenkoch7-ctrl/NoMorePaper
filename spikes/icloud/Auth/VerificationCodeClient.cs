using System.Text.Json.Nodes;
using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>The second factor: request a code, have Apple check it, mark this client as trusted.</summary>
internal sealed class VerificationCodeClient
{
    private const string DeviceCodePath = "/verify/trusteddevice/securitycode";

    private readonly AuthChannel _channel;

    public VerificationCodeClient(AuthChannel channel) => _channel = channel;

    /// <summary>Reads the phone numbers for the SMS fallback. An empty list is a valid answer.</summary>
    public async Task<IReadOnlyList<TrustedPhone>> GetTrustedPhonesAsync(CancellationToken cancellationToken)
    {
        var page = await _channel.SendAsync(
            HttpMethod.Get, string.Empty, body: null, AuthChannel.HtmlAccept, cancellationToken);
        var phones = TrustedPhoneParser.Parse(page.Body);
        if (phones.Count > 0)
        {
            return phones;
        }

        var json = await _channel.SendAsync(
            HttpMethod.Get, string.Empty, body: null, AuthChannel.JsonAccept, cancellationToken);
        return TrustedPhoneParser.Parse(json.Body);
    }

    /// <summary>
    /// Asks Apple to show a code on the trusted devices. Since spring 2026 the code is no longer
    /// pushed on its own after the password step; this empty PUT is what triggers it.
    /// </summary>
    public async Task<bool> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        var response = await _channel.SendAsync(
            HttpMethod.Put, DeviceCodePath, string.Empty, AuthChannel.JsonAccept, cancellationToken);
        return response.IsSuccess;
    }

    public async Task RequestSmsCodeAsync(int phoneId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["phoneNumber"] = new JsonObject { ["id"] = phoneId }, ["mode"] = "sms" };
        var response = await _channel.SendAsync(
            HttpMethod.Put, "/verify/phone", body.ToJsonString(), AuthChannel.JsonAccept, cancellationToken);
        if (!response.IsSuccess)
        {
            throw ResponseErrors.Create("verify/phone", response);
        }
    }

    /// <summary>Returns false for a wrong code; throws for everything else that is not a success.</summary>
    public async Task<bool> VerifyDeviceCodeAsync(string code, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["securityCode"] = new JsonObject { ["code"] = code } };
        var response = await _channel.SendAsync(
            HttpMethod.Post, DeviceCodePath, body.ToJsonString(), AuthChannel.JsonAccept, cancellationToken);
        return IsCodeAccepted("verify/trusteddevice/securitycode", response);
    }

    public async Task<bool> VerifySmsCodeAsync(int phoneId, string code, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["phoneNumber"] = new JsonObject { ["id"] = phoneId },
            ["securityCode"] = new JsonObject { ["code"] = code },
            ["mode"] = "sms",
        };
        var response = await _channel.SendAsync(
            HttpMethod.Post, "/verify/phone/securitycode", body.ToJsonString(), AuthChannel.JsonAccept, cancellationToken);
        return IsCodeAccepted("verify/phone/securitycode", response);
    }

    /// <summary>Makes Apple issue the trust token and a session token for the now trusted session.</summary>
    public async Task TrustSessionAsync(CancellationToken cancellationToken)
    {
        var response = await _channel.SendAsync(
            HttpMethod.Get, "/2sv/trust", body: null, AuthChannel.ScriptAccept, cancellationToken);
        if (!response.IsSuccess)
        {
            throw ResponseErrors.Create("2sv/trust", response);
        }
    }

    private static bool IsCodeAccepted(string step, ApiResponse response)
    {
        // Apple also reports an accepted code with 409 ("securityCode.valid") or, if the account
        // needs attention in the browser, with 412 plus a repair token.
        var accepted = response.IsSuccess
            || (response.Status == 409 && response.Json.Child("securityCode").Child("valid").Flag() == true)
            || (response.Status == 412 && response.Header("X-Apple-Repair-Session-Token") is not null);
        if (accepted)
        {
            return true;
        }

        return AppleError.From(response).Code == AppleError.WrongSecurityCode
            ? false
            : throw ResponseErrors.Create(step, response);
    }
}
