namespace NoMorePaper.Contracts.Accounts;

/// <summary>An invitation or reset link to hand over; it works once, until <paramref name="ExpiresAt"/>.</summary>
/// <param name="Link">Absolute URL of the page in the app (<c>/einladung/{token}</c> or <c>/passwort/{token}</c>).</param>
public sealed record LinkDto(string Link, DateTimeOffset ExpiresAt);
