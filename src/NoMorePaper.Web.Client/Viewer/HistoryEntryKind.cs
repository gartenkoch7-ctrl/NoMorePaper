namespace NoMorePaper.Web.Client.Viewer;

/// <summary>What one entry of the history tab says; each kind has its own title text.</summary>
public enum HistoryEntryKind
{
    /// <summary>The document came in (the only entry with a time and a source).</summary>
    Received,

    /// <summary>Waiting to be processed.</summary>
    Queued,

    /// <summary>Being processed right now.</summary>
    Processing,

    /// <summary>Processing finished, the text was recognized.</summary>
    TextRecognized,

    /// <summary>Processing failed.</summary>
    Failed,

    /// <summary>The details were recognized automatically and are not checked yet.</summary>
    FieldsRecognized,

    /// <summary>A person checked and confirmed the details.</summary>
    FieldsConfirmed,
}
