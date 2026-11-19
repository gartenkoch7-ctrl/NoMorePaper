namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// What the own mail server found about the sender address (SPF/DMARC in its <c>Authentication-Results</c>
/// header). Only <see cref="Passed"/> lets a sender address count for an account; anything else makes the
/// sender unknown, so a forged sender neither skips the spam check nor reaches an account.
/// </summary>
public enum SenderCheck
{
    /// <summary>DMARC or SPF passed for the domain of the sender address.</summary>
    Passed,

    /// <summary>DMARC or SPF failed for the domain of the sender address.</summary>
    Failed,

    /// <summary>No verdict of the trusted server, or none for this domain.</summary>
    Unknown,
}
