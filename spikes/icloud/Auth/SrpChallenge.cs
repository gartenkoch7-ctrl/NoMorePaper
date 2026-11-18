using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>Apple's answer to "signin/init".</summary>
/// <param name="Salt">Salt for both the password key and the SRP exponent.</param>
/// <param name="ServerPublicKey">B, big-endian.</param>
/// <param name="Continuation">Opaque value "c" that must be echoed in "signin/complete".</param>
/// <param name="Iterations">PBKDF2 iteration count.</param>
/// <param name="Protocol">"s2k" or "s2k_fo", see <see cref="ApplePasswordKey"/>.</param>
internal sealed record SrpChallenge(
    byte[] Salt, byte[] ServerPublicKey, string Continuation, int Iterations, string Protocol)
{
    public static SrpChallenge Parse(ApiResponse response)
    {
        var json = response.Json;
        var salt = json.Child("salt").Text();
        var serverPublicKey = json.Child("b").Text();
        var continuation = json.Child("c").Text();
        var iterations = json.Child("iteration").Number();
        var protocol = json.Child("protocol").Text();
        if (salt is null || serverPublicKey is null || continuation is null || protocol is null
            || iterations is null or < 1 or > int.MaxValue)
        {
            throw new ICloudException(
                ICloudFailure.ProtocolChanged, "signin/init: the answer lacks the expected fields.", response.Status);
        }

        try
        {
            return new SrpChallenge(
                Convert.FromBase64String(salt),
                Convert.FromBase64String(serverPublicKey),
                continuation,
                (int)iterations.Value,
                protocol);
        }
        catch (FormatException exception)
        {
            throw new ICloudException(
                ICloudFailure.ProtocolChanged,
                "signin/init: salt or server value is not Base64.",
                response.Status,
                innerException: exception);
        }
    }
}
