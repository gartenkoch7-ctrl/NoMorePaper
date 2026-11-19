namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>A row of <c>account_mail_senders</c>: a normalized address and the one account it belongs to.</summary>
internal sealed class MailSenderRow
{
    public string Address { get; set; } = string.Empty;

    public Guid AccountId { get; set; }
}
