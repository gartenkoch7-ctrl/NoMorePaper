namespace ICloudSpike.Http;

/// <summary>Why a step failed, in terms the user (and later the app) can act on.</summary>
internal enum ICloudFailure
{
    /// <summary>Anything not recognised; the status code and Apple's error code are attached.</summary>
    Unexpected,

    /// <summary>No connection, DNS failure or timeout.</summary>
    Network,

    /// <summary>Apple rejected Apple-ID or password (HTTP 401 on sign-in).</summary>
    WrongCredentials,

    /// <summary>Apple locked the account (service error -20209).</summary>
    AccountLocked,

    /// <summary>Apple wants the user to confirm something in the browser first (HTTP 412).</summary>
    RepairRequired,

    /// <summary>Apple throttles the client (HTTP 429 or 503).</summary>
    RateLimited,

    /// <summary>Cookies or session token are no longer accepted (HTTP 401, 421 or 450 on a service).</summary>
    SessionExpired,

    /// <summary>The verification code step did not end in a trusted session.</summary>
    TwoFactorFailed,

    /// <summary>The account does not allow web access to iCloud Drive (Advanced Data Protection).</summary>
    WebAccessBlocked,

    /// <summary>The answer does not look like the documented protocol any more.</summary>
    ProtocolChanged,

    /// <summary>The user ended the dialogue.</summary>
    Cancelled,
}
