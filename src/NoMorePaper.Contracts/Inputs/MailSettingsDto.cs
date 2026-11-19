namespace NoMorePaper.Contracts.Inputs;

/// <summary>The household's mailbox as set up by the administrator; never the password.</summary>
/// <param name="Security"><c>ssl</c> or <c>starttls</c>; an unencrypted connection is never accepted.</param>
/// <param name="TrustedAuthServId">
/// The <c>authserv-id</c> of the own mail server in <c>Authentication-Results</c>; only its verdicts on
/// SPF/DMARC count. <c>null</c>: no sender counts as checked.
/// </param>
/// <param name="HasPassword">A password is stored (encrypted).</param>
public sealed record MailSettingsDto(string Host, int Port, string Security, string UserName, string? TrustedAuthServId, bool HasPassword);
