namespace NoMorePaper.Domain.Classification;

/// <summary>Who sent a document, e.g. "Stadtwerke München".</summary>
public sealed class Correspondent : ClassificationEntry
{
    private Correspondent(Guid id, string name, Guid createdBy)
        : base(id, name, createdBy)
    {
    }

    /// <param name="name">Name as entered or recognized; trimmed.</param>
    /// <param name="createdBy">The account that created it.</param>
    /// <param name="createdAt">Time from the caller's clock; the id derives from it.</param>
    /// <exception cref="ArgumentException">The name is invalid or there is no creator.</exception>
    public static Correspondent Create(string name, Guid createdBy, DateTimeOffset createdAt) => new(NewId(createdAt), name, createdBy);
}
