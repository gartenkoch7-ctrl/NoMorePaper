namespace NoMorePaper.Application.Inputs;

/// <summary>The last known state of an input, or of one account's part of it.</summary>
/// <param name="AccountId">The account the row is about, or <c>null</c> for the whole input.</param>
/// <param name="LastRunAt">When the last run ended.</param>
/// <param name="LastSuccessAt">When a run last ended without problem.</param>
/// <param name="Problem">The problem of the last run, or <c>null</c>.</param>
/// <param name="ReceivedTotal">Documents taken in since the row exists.</param>
public sealed record InputStatus(
    InputKind Kind,
    Guid? AccountId,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? LastSuccessAt,
    InputProblemKind? Problem,
    bool NeedsAttention,
    long ReceivedTotal);
