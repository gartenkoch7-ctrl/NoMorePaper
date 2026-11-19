namespace NoMorePaper.Domain.Documents;

/// <summary>How a document came in (M4, ADR-0024); the archive treats all of them alike.</summary>
public enum DocumentSource
{
    /// <summary>Uploaded in the browser.</summary>
    Upload,

    /// <summary>Photographed with the camera of the PWA; the pages are joined into one PDF.</summary>
    Camera,

    /// <summary>Put into the scanner folder (SMB share).</summary>
    ScannerFolder,

    /// <summary>Attachment or text of a mail to the household's own mail address.</summary>
    Mail,

    /// <summary>Read from a chosen folder of an account's iCloud Drive.</summary>
    ICloud,
}
