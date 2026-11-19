namespace NoMorePaper.Contracts.Inputs;

/// <summary>
/// The signed-in account's preferences for its inputs (<c>GET/PUT /api/account/preferences</c>). A refused
/// address answers 400 (invalid) or 409 (another account has it) with <c>errors: { mailSenders: [word] }</c>.
/// </summary>
/// <param name="NewDocumentsPrivate">New documents of the background inputs (and the default of the upload) are private.</param>
/// <param name="MailSenders">Addresses whose mails belong to the account; at most 20, each belongs to one account only.</param>
public sealed record AccountPreferencesDto(bool NewDocumentsPrivate, IReadOnlyList<string> MailSenders);
