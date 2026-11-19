namespace NoMorePaper.Application.Classification;

/// <summary>Maps the store's <see cref="EntryChange"/> to the use-case outcome. Pure logic.</summary>
internal static class EntryOutcomes
{
    public static ChangeOutcome From(EntryChange change) => change switch
    {
        EntryChange.Changed => ChangeOutcome.Changed,
        EntryChange.NotFound => ChangeOutcome.NotFound,
        EntryChange.Forbidden => ChangeOutcome.Forbidden,
        _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Unknown entry change."),
    };
}
