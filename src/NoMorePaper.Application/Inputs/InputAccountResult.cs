namespace NoMorePaper.Application.Inputs;

/// <summary>
/// What a run did for one account's part of an input (e.g. its iCloud). Recorded in that account's
/// status row, so a later success clears that account's problem and its documents count for it.
/// </summary>
/// <param name="Problem">Why this account's part did not get through, or <c>null</c> when it did.</param>
public sealed record InputAccountResult(Guid AccountId, int Received, int Skipped, InputProblem? Problem = null);
