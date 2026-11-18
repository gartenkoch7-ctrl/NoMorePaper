namespace ICloudSpike.Auth;

/// <summary>Which of the sign-in routes led to a usable session. Decides the spike's restart criterion.</summary>
internal enum LoginPath
{
    /// <summary>Stored cookies were still valid; nothing was typed.</summary>
    ReusedSession,

    /// <summary>Cookies had expired, the stored session token produced new ones; nothing was typed.</summary>
    RenewedByToken,

    /// <summary>Password was needed, but the stored trust token spared the verification code.</summary>
    PasswordOnly,

    /// <summary>Password and verification code were needed.</summary>
    PasswordAndCode,
}
