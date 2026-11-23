namespace NoMorePaper.Application.Editing;

/// <summary>Which file a <see cref="FinishMove"/> takes to the trash.</summary>
public enum FinishMoveKind
{
    /// <summary>The previous edited version; only the newest one is kept.</summary>
    PreviousEdited,

    /// <summary>The new edited version itself, because the document keeps only its original.</summary>
    NewEdited,

    /// <summary>The original, because the document keeps only its edited version.</summary>
    Original,
}
