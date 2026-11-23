namespace NoMorePaper.Application.Trash;

/// <summary>How long the trash keeps what it holds (ADR-0025). Pure logic.</summary>
public static class TrashRules
{
    /// <summary>After this time an entry is removed for good.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>When an entry put into the trash at <paramref name="at"/> is removed for good, in UTC.</summary>
    public static DateTimeOffset PurgeAfter(DateTimeOffset at) => at.ToUniversalTime() + Retention;
}
