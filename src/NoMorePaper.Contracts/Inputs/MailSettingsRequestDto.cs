namespace NoMorePaper.Contracts.Inputs;

/// <summary>Saves the household's mailbox (administrator only); see <see cref="MailSettingsDto"/>.</summary>
/// <param name="Password">A new password; <c>null</c>, empty or blank keeps the stored one.</param>
public sealed record MailSettingsRequestDto(
    string Host, int Port, string Security, string UserName, string? TrustedAuthServId, string? Password);
