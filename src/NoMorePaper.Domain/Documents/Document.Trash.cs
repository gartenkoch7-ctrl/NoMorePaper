namespace NoMorePaper.Domain.Documents;

// The trash (ADR-0025): a deleted document stays as a row for 30 days, hidden everywhere but on its
// owner's trash page, and can be restored until it is removed for good.
public sealed partial class Document
{
    /// <summary>When the document was moved to the trash, in UTC; <c>null</c> while it is not there.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Who moved it to the trash (its owner); <c>null</c> while it is not there.</summary>
    public Guid? DeletedBy { get; private set; }

    /// <summary>Whether the document lies in the trash; it is then neither visible nor editable.</summary>
    public bool IsInTrash => DeletedAt is not null;

    /// <summary>Moves the document to the trash; its files stay where they are.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner, or it is in the trash already.</exception>
    public void MoveToTrash(Guid actor, DateTimeOffset at)
    {
        RequireOwner(actor);
        if (IsInTrash)
        {
            throw new InvalidOperationException($"Document {Id} is in the trash already.");
        }

        DeletedAt = at.ToUniversalTime();
        DeletedBy = actor;
    }

    /// <summary>Takes the document out of the trash; it is visible and editable as before.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner, or it is not in the trash.</exception>
    public void RestoreFromTrash(Guid actor)
    {
        RequireOwner(actor);
        if (!IsInTrash)
        {
            throw new InvalidOperationException($"Document {Id} is not in the trash.");
        }

        DeletedAt = null;
        DeletedBy = null;
    }
}
