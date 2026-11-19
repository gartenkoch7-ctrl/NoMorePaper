namespace NoMorePaper.Application.Security;

/// <summary>The purposes of <see cref="ISecretProtector"/>; each gets keys of its own.</summary>
public static class SecretPurposes
{
    /// <summary>Password of the household's mailbox (IMAP).</summary>
    public const string MailPassword = "MailPassword";

    /// <summary>Session of an account's Apple ID (iCloud Drive).</summary>
    public const string ICloudSession = "ICloudSession";
}
