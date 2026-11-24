using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Domain.Documents;

// Scan editing (ADR-0025): which versions the document keeps, the suggestion to split a scanned stack, and
// whether the document is a part of a split one.
public sealed partial class Document
{
    /// <summary>The owner's choice for this document; <c>null</c> means the owner's account default applies.</summary>
    public VersionPolicy? VersionPolicy { get; private set; }

    /// <summary>Where a scanned stack probably splits into several documents; <c>null</c> when there is no suggestion.</summary>
    public SplitSuggestion? SplitSuggestion { get; private set; }

    /// <summary>Chooses which versions the document keeps; the files follow by the caller (trash, rebuild).</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="policy"/> is unknown.</exception>
    public void ChooseVersions(Guid actor, VersionPolicy policy)
    {
        RequireOwner(actor);
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown version policy.");
        }

        VersionPolicy = policy;
    }

    /// <summary>Notes where the scanned stack probably splits; replaces an earlier suggestion.</summary>
    public void SuggestSplit(SplitSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        SplitSuggestion = suggestion;
    }

    /// <summary>
    /// The document this one was split from; <c>null</c> unless it is a part of a split. A part is never
    /// suggested for separation again: the split already settled its pages.
    /// </summary>
    public Guid? SplitFrom { get; private set; }

    /// <summary>Notes that the document is a part of the split document <paramref name="source"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="source"/> is empty or the document itself.</exception>
    public void MarkSplitPart(Guid source)
    {
        if (source == Guid.Empty || source == Id)
        {
            throw new ArgumentException("A part is split from another document.", nameof(source));
        }

        SplitFrom = source;
    }

    /// <summary>Drops the suggestion, e.g. because the stack is one document after all.</summary>
    public void DismissSplit() => SplitSuggestion = null;
}
