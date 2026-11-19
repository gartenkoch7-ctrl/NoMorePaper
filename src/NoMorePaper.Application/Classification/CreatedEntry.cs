namespace NoMorePaper.Application.Classification;

/// <summary>Outcome of <see cref="CreateEntry"/>.</summary>
/// <param name="Entry">The found or new entry when <paramref name="Outcome"/> is <see cref="ChangeOutcome.Changed"/>.</param>
public sealed record CreatedEntry(ChangeOutcome Outcome, EntrySummary? Entry);
