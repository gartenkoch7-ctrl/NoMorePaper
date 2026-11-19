using NoMorePaper.Application.Inputs.Mail;
using MimeResults = MimeKit.Cryptography.AuthenticationResults;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// Reads the verdict of the own mail server on the sender from <c>Authentication-Results</c> (RFC 8601). A
/// sender may put any such header into its mail, so only a header with the <c>authserv-id</c> the
/// administrator entered counts, and of those only the topmost: the own server adds its header above
/// everything that came with the mail. Pure logic.
/// </summary>
public static class AuthenticationResults
{
    /// <param name="headers">Raw values of the <c>Authentication-Results</c> headers, top to bottom.</param>
    /// <param name="senderDomain">The domain of the <c>From</c> address.</param>
    /// <param name="trustedAuthServId">The own server's <c>authserv-id</c>; <c>null</c> trusts nobody.</param>
    /// <returns>
    /// <see cref="SenderCheck.Passed"/> when DMARC or SPF passed for <paramref name="senderDomain"/> and neither
    /// failed; <see cref="SenderCheck.Failed"/> when one failed; otherwise <see cref="SenderCheck.Unknown"/>.
    /// </returns>
    public static SenderCheck Evaluate(IEnumerable<byte[]> headers, string senderDomain, string? trustedAuthServId)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(senderDomain);
        if (string.IsNullOrWhiteSpace(trustedAuthServId) || senderDomain.Length == 0)
        {
            return SenderCheck.Unknown;
        }

        foreach (var raw in headers)
        {
            if (MimeResults.TryParse(raw, out var results)
                && string.Equals(results.AuthenticationServiceIdentifier, trustedAuthServId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Verdict(results, senderDomain);
            }
        }

        return SenderCheck.Unknown;
    }

    private static SenderCheck Verdict(MimeResults results, string senderDomain)
    {
        var passed = false;
        foreach (var result in results.Results)
        {
            var method = result.Method?.ToLowerInvariant();
            var domain = method switch
            {
                // DMARC is about the From domain by definition; a named domain must be ours.
                "dmarc" => Property(result, "header", "from") ?? senderDomain,

                // SPF checks the envelope sender, which counts only when it is the From domain (alignment).
                "spf" => Property(result, "smtp", "mailfrom"),
                _ => null,
            };
            if (domain is null || !string.Equals(MailText.Domain(domain.Contains('@', StringComparison.Ordinal) ? domain : "@" + domain), senderDomain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (result.Result?.ToLowerInvariant())
            {
                case "pass":
                    passed = true;
                    break;
                case "fail" or "softfail" or "permerror":
                    return SenderCheck.Failed;
            }
        }

        return passed ? SenderCheck.Passed : SenderCheck.Unknown;
    }

    private static string? Property(MimeKit.Cryptography.AuthenticationMethodResult result, string type, string name) =>
        result.Properties
            .FirstOrDefault(property => string.Equals(property.PropertyType, type, StringComparison.OrdinalIgnoreCase)
                && string.Equals(property.Property, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
}
