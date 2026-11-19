namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>What <see cref="ScannerFileIntake"/> did with one finished file.</summary>
public enum ScannerFileOutcome
{
    /// <summary>A new document; the file is deleted (or, if that failed, dropped as a duplicate next run).</summary>
    Accepted,

    /// <summary>The owner has this file already; it is deleted without a second document.</summary>
    Duplicate,

    /// <summary>Not a PDF, JPEG, PNG or HEIC; moved to the error folder.</summary>
    MovedToErrors,

    /// <summary>Left where it is on purpose: owner removed meanwhile, no administrator, or changed while read.</summary>
    Kept,

    /// <summary>Could not be taken in or moved (database, disk); it stays for the next run.</summary>
    Failed,
}
