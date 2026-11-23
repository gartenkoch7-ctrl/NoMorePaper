namespace NoMorePaper.Contracts.Inputs;

/// <summary>The state of an input, or of one account's part of it (<c>GET /api/inputs</c>).</summary>
/// <param name="Kind"><c>scanner-folder</c>, <c>mail</c> or <c>icloud</c>.</param>
/// <param name="AccountName">Display name of the account the row is about; <c>null</c> for the whole input.</param>
/// <param name="State"><c>ok</c> when the last run got through, <c>problem</c> otherwise.</param>
/// <param name="Problem">
/// The problem of the last run in kebab case (<c>not-configured</c>, <c>sign-in-failed</c>, <c>code-needed</c>,
/// <c>unreachable</c>, <c>llm-unavailable</c>, <c>throttled</c>, <c>protocol-changed</c>, <c>unknown-folder</c>,
/// <c>stuck-items</c>, <c>failed</c>); <c>null</c> without problem.
/// </param>
/// <param name="NeedsAttention">Somebody has to act; waiting does not help.</param>
/// <param name="ReceivedTotal">Documents taken in since the row exists.</param>
public sealed record InputStatusDto(
    string Kind,
    string? AccountName,
    string State,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? LastSuccessAt,
    string? Problem,
    bool NeedsAttention,
    long ReceivedTotal = 0);
