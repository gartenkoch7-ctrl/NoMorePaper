namespace NoMorePaper.Application.Inputs;

/// <summary>What an input remembers about an item it has seen (<see cref="ISeenItems"/>).</summary>
public enum SeenList
{
    /// <summary>Taken in; never taken twice (e.g. a mail whose move to "processed" failed).</summary>
    Imported,

    /// <summary>Judged as spam once; back in the inbox it is taken without a second check.</summary>
    Spam,

    /// <summary>Failed; the row counts the failures.</summary>
    Failed,

    /// <summary>
    /// Judged as spam, but not yet in the spam folder (its move failed): the next run only retries the move,
    /// without a second LLM check, and the mail is not taken as one moved back from the spam folder.
    /// </summary>
    SpamMovePending,
}
