namespace NoMorePaper.Domain.Documents;

/// <summary>Where a document stands in the processing pipeline (OCR, PDF/A).</summary>
public enum ProcessingState
{
    /// <summary>Received and waiting for the Worker.</summary>
    Queued,

    /// <summary>A Worker is working on it, or will retry after a failed attempt.</summary>
    Processing,

    /// <summary>Archive file and text are available.</summary>
    Done,

    /// <summary>Every attempt failed; the reason is technical and contains no document content.</summary>
    Failed,
}
