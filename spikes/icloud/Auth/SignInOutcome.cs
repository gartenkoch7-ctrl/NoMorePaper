namespace ICloudSpike.Auth;

/// <summary>How Apple answered a correct password.</summary>
internal enum SignInOutcome
{
    /// <summary>HTTP 200: the stored trust token was accepted, no code is needed.</summary>
    Authenticated,

    /// <summary>HTTP 409: a verification code is needed before the session is trusted.</summary>
    TwoFactorRequired,
}
