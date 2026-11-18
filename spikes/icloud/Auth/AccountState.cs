using System.Text.Json;
using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>What iCloud reports about the signed-in account ("accountLogin" and "validate").</summary>
/// <param name="Dsid">Apple's numeric account id; a required parameter of every service call.</param>
/// <param name="HsaVersion">0 = no second factor, 1 = old two-step verification, 2 = 2FA.</param>
/// <param name="ChallengeRequired">Apple wants a verification code for this session.</param>
/// <param name="TrustedBrowser">This client is trusted, i.e. 2FA has been passed.</param>
/// <param name="DriveUrl">Base address for folder listings ("drivews"); null if not offered.</param>
/// <param name="DocumentsUrl">Base address for downloads ("docws"); null if not offered.</param>
/// <param name="WebAccessAllowed">False if the account blocks iCloud web access (Advanced Data Protection).</param>
/// <param name="AdvancedProtection">True if end-to-end encryption for iCloud data is on ("iCDPEnabled").</param>
/// <param name="DomainToUse">Set for accounts that live on another iCloud domain (mainland China).</param>
internal sealed record AccountState(
    string? Dsid,
    int HsaVersion,
    bool ChallengeRequired,
    bool TrustedBrowser,
    string? DriveUrl,
    string? DocumentsUrl,
    bool? WebAccessAllowed,
    bool? AdvancedProtection,
    string? DomainToUse)
{
    public bool RequiresTwoFactor => HsaVersion >= 1 && (ChallengeRequired || !TrustedBrowser);

    public static AccountState Parse(JsonElement root)
    {
        JsonElement? json = root;
        var account = json.Child("dsInfo");
        var services = json.Child("webservices");
        return new AccountState(
            account.Child("dsid").Text(),
            (int)(account.Child("hsaVersion").Number() ?? 0),
            json.Child("hsaChallengeRequired").Flag() ?? false,
            json.Child("hsaTrustedBrowser").Flag() ?? false,
            services.Child("drivews").Child("url").Text(),
            services.Child("docws").Child("url").Text(),
            account.Child("isWebAccessAllowed").Flag() ?? json.Child("isWebAccessAllowed").Flag(),
            json.Child("iCDPEnabled").Flag() ?? account.Child("iCDPEnabled").Flag(),
            json.Child("domainToUse").Text());
    }
}
