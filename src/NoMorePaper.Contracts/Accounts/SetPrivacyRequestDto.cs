namespace NoMorePaper.Contracts.Accounts;

/// <summary>Makes a document private (only its owner sees it) or shared again.</summary>
public sealed record SetPrivacyRequestDto(bool IsPrivate);
