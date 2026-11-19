namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// Whose a mail is and whether it needs the spam check (ADR-0024). Pure logic.
/// </summary>
/// <param name="OwnerId">The account of the sender, or <c>null</c>: unknown, so the administrator gets it for the owner check.</param>
/// <param name="NeedsSpamCheck">The LLM decides about spam; mails of accounts and mails back from the spam folder never.</param>
public sealed record MailRouting(Guid? OwnerId, bool NeedsSpamCheck)
{
    /// <param name="accountOfSender">The account that entered the sender address, if any.</param>
    /// <param name="check">What the own mail server found about the sender address.</param>
    /// <param name="backFromSpam">The mail was judged as spam once and has been moved back into the inbox.</param>
    public static MailRouting Decide(Guid? accountOfSender, SenderCheck check, bool backFromSpam)
    {
        // An address alone is easy to forge: only a sender the own server confirmed counts for an account.
        var owner = check == SenderCheck.Passed ? accountOfSender : null;
        return new MailRouting(owner, owner is null && !backFromSpam);
    }
}
