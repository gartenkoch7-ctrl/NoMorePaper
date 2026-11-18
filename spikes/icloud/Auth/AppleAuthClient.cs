using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>The password step of the web sign-in: proves the password to Apple via SRP.</summary>
internal sealed class AppleAuthClient
{
    private readonly AuthChannel _channel;

    public AppleAuthClient(AuthChannel channel) => _channel = channel;

    /// <summary>
    /// Loads the sign-in frame like a browser does before the password is sent. Its only purpose is
    /// to pick up Apple's continuation headers; the answer itself is not needed.
    /// </summary>
    public async Task PreflightAsync(CancellationToken cancellationToken)
    {
        var path = ApiRequest.AppendQuery("/authorize/signin", new Dictionary<string, string>
        {
            ["frame_id"] = _channel.ClientId,
            ["iframeid"] = _channel.ClientId,
            ["state"] = _channel.ClientId,
            ["skVersion"] = "7",
            ["client_id"] = AuthChannel.WidgetKey,
            ["response_type"] = "code",
            ["redirect_uri"] = ICloudTransport.HomeUrl,
            ["response_mode"] = "web_message",
            ["authVersion"] = "latest",
        });
        await _channel.SendAsync(HttpMethod.Get, path, body: null, AuthChannel.HtmlAccept, cancellationToken);
    }

    /// <summary>Proves knowledge of the password without sending it (SRP-6a, 2048 bit, SHA-256).</summary>
    public async Task<SignInOutcome> SignInAsync(
        string accountName, ReadOnlyMemory<byte> passwordUtf8, CancellationToken cancellationToken)
    {
        var privateKey = RandomNumberGenerator.GetBytes(32);
        var srp = new SrpClient(SrpGroup.Rfc5054Bits2048, HashAlgorithmName.SHA256, privateKey);
        CryptographicOperations.ZeroMemory(privateKey);

        var initBody = new JsonObject
        {
            ["a"] = Convert.ToBase64String(srp.PublicKeyBytes),
            ["accountName"] = accountName,
            ["protocols"] = new JsonArray(ApplePasswordKey.ProtocolS2k, ApplePasswordKey.ProtocolS2kFo),
        };
        var init = await _channel.SendAsync(
            HttpMethod.Post, "/signin/init", initBody.ToJsonString(), AuthChannel.ScriptAccept, cancellationToken);
        if (!init.IsSuccess)
        {
            throw SignInError("signin/init", init);
        }

        var challenge = SrpChallenge.Parse(init);
        var key = ApplePasswordKey.Derive(passwordUtf8.Span, challenge.Protocol, challenge.Salt, challenge.Iterations);
        SrpProof proof;
        try
        {
            proof = srp.CreateProof(accountName, string.Empty, key, challenge.Salt, challenge.ServerPublicKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        // A trust token from an earlier 2FA lets Apple answer 200 instead of asking for a code again.
        var trustTokens = new JsonArray();
        if (_channel.TrustToken is { } trustToken)
        {
            trustTokens.Add(trustToken);
        }

        var completeBody = new JsonObject
        {
            ["accountName"] = accountName,
            ["c"] = challenge.Continuation,
            ["m1"] = Convert.ToBase64String(proof.ClientProof),
            ["m2"] = Convert.ToBase64String(proof.ExpectedServerProof),
            ["rememberMe"] = true,
            ["trustTokens"] = trustTokens,
        };
        var complete = await _channel.SendAsync(
            HttpMethod.Post,
            "/signin/complete?isRememberMeEnabled=true",
            completeBody.ToJsonString(),
            AuthChannel.ScriptAccept,
            cancellationToken);
        return complete.Status switch
        {
            >= 200 and < 300 => SignInOutcome.Authenticated,
            409 => SignInOutcome.TwoFactorRequired,
            412 => throw ResponseErrors.Create("signin/complete", complete, ICloudFailure.RepairRequired),
            _ => throw SignInError("signin/complete", complete),
        };
    }

    private static ICloudException SignInError(string step, ApiResponse response)
    {
        var error = AppleError.From(response);
        var rejected = response.Status == 401 || error.Code == AppleError.WrongCredentials;
        var locked = error.Code == AppleError.AccountLocked;
        return ResponseErrors.Create(step, response, rejected && !locked ? ICloudFailure.WrongCredentials : null);
    }
}
