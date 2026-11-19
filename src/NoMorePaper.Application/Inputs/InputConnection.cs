namespace NoMorePaper.Application.Inputs;

/// <summary>The stored set-up of an input, or of one account's part of it (e.g. its iCloud session).</summary>
/// <param name="AccountId">The account it belongs to, or <c>null</c> for the household's (the mailbox).</param>
/// <param name="Settings">Settings without secrets as JSON text, read by the input itself.</param>
/// <param name="Secret">
/// The secret (password, session), encrypted already with <see cref="Security.ISecretProtector"/>; never
/// plain text. <c>null</c> when none is stored.
/// </param>
public sealed record InputConnection(InputKind Kind, Guid? AccountId, string Settings, byte[]? Secret, DateTimeOffset UpdatedAt);
