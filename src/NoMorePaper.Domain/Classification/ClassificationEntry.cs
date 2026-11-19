namespace NoMorePaper.Domain.Classification;

/// <summary>
/// An entry of a maintained list (<see cref="ClassificationKind"/>). Who may see it follows from the
/// documents that use it: an entry used only in private documents stays as private as they are; an
/// unused entry is seen only by the account that created it.
/// </summary>
public abstract class ClassificationEntry
{
    /// <exception cref="ArgumentException">The name is invalid (<see cref="ClassificationName.Clean"/>) or there is no creator.</exception>
    protected ClassificationEntry(Guid id, string name, Guid createdBy)
    {
        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("An entry needs the account that created it.", nameof(createdBy));
        }

        Id = id;
        CreatedBy = createdBy;
        Name = ClassificationName.Clean(name, nameof(name));
        NormalizedName = ClassificationName.Normalize(Name);
    }

    /// <summary>Version 7 GUID, sortable by creation time.</summary>
    public Guid Id { get; }

    /// <summary>Name as shown, trimmed.</summary>
    public string Name { get; private set; }

    /// <summary>Comparison key of <see cref="Name"/> (<see cref="ClassificationName.Normalize"/>).</summary>
    public string NormalizedName { get; private set; }

    /// <summary>Account that created the entry; it sees the entry while no document uses it.</summary>
    public Guid CreatedBy { get; }

    /// <summary>Changes the name; whether the actor may do so depends on the documents and is checked by the caller.</summary>
    /// <exception cref="ArgumentException">The name is invalid (<see cref="ClassificationName.Clean"/>).</exception>
    public void Rename(string name)
    {
        Name = ClassificationName.Clean(name, nameof(name));
        NormalizedName = ClassificationName.Normalize(Name);
    }

    /// <summary>A new id for an entry created at <paramref name="createdAt"/> (from the caller's clock).</summary>
    protected static Guid NewId(DateTimeOffset createdAt) => Guid.CreateVersion7(createdAt.ToUniversalTime());
}
