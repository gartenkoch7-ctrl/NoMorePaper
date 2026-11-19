namespace NoMorePaper.Application.Accounts;

/// <summary>Outcome of <see cref="ChangePreferences"/>.</summary>
/// <param name="Address">The refused address (<see cref="PreferencesOutcome.Invalid"/>, <see cref="PreferencesOutcome.SenderTaken"/>), as the account entered it.</param>
public sealed record PreferencesResult(PreferencesOutcome Outcome, string? Address = null);
