namespace ICloudSpike.SelfTest;

/// <summary>
/// State of the simulated Apple account. All values are invented; every secret carries the marker
/// <see cref="SecretMarker"/> so a test can prove that none of them reaches console or trace.
/// </summary>
internal sealed class FakeAppleAccount
{
    public const string SecretMarker = "s3cr3t";
    public const string AccountName = "spike@example.com";
    public const string Password = "Tr0ub4dor&3-äöü-" + SecretMarker;
    public const string DeviceCode = "123456";
    public const string SmsCode = "654321";
    public const string Dsid = "10000000042";
    public const string TrustToken = "TRUST-" + SecretMarker;
    public const string UntrustedSessionToken = "SESSION-untrusted-" + SecretMarker;

    private int _tokenGeneration;
    private int _cookieGeneration;

    /// <summary>"s2k" or "s2k_fo".</summary>
    public string Protocol { get; set; } = "s2k";

    /// <summary>If set, "signin/init" answers with this status (throttling).</summary>
    public int? InitStatus { get; set; }

    /// <summary>If set, the folder listing answers with this status.</summary>
    public int? DriveStatus { get; set; }

    /// <summary>False simulates Advanced Data Protection with web access switched off.</summary>
    public bool WebAccessAllowed { get; set; } = true;

    public bool Locked { get; set; }

    public bool CodeVerified { get; set; }

    public int PushRequests { get; set; }

    public int SmsRequests { get; set; }

    public string TrustedSessionToken => $"SESSION-trusted-{_tokenGeneration}-{SecretMarker}";

    public string WebAuthCookie => $"WEBAUTH-{_cookieGeneration}-{SecretMarker}";

    /// <summary>Cookies stop working, the session token still does.</summary>
    public void ExpireCookies() => _cookieGeneration++;

    /// <summary>Cookies and session token stop working; only the trust token survives.</summary>
    public void ExpireSession()
    {
        _cookieGeneration++;
        _tokenGeneration++;
    }
}
