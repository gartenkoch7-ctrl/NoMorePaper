namespace NoMorePaper.Application.Documents;

/// <summary>How <see cref="IntakeDocument"/> ended.</summary>
public enum IntakeStatus
{
    /// <summary>Stored and queued for processing.</summary>
    Received,

    /// <summary>A document with exactly this file exists in the checked scope (<see cref="DuplicateScope"/>); nothing was kept.</summary>
    AlreadyThere,

    /// <summary>The given owner was removed; nothing was stored.</summary>
    OwnerGone,

    /// <summary>The owner is unknown and there is no administrator to take it (household not set up); nothing was stored.</summary>
    NoAdministrator,
}
