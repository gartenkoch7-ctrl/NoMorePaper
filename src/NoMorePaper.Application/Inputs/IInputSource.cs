namespace NoMorePaper.Application.Inputs;

/// <summary>
/// A background input the Worker's scheduler runs in its interval (scanner folder, mail, iCloud). Each
/// run gets a scope of its own; one failing input never stops the others. Implementations take files
/// in through <see cref="Documents.IntakeDocument"/> with <see cref="Documents.DuplicateScope.Owned"/>.
/// </summary>
public interface IInputSource
{
    /// <summary>Which input this is; at most one source per kind.</summary>
    InputKind Kind { get; }

    /// <summary>Time from the start of one run to the start of the next.</summary>
    TimeSpan Interval { get; }

    /// <summary>Takes in what is new. Problems the source knows (e.g. not set up) are part of the result, not exceptions.</summary>
    /// <exception cref="OperationCanceledException">The Worker is stopping.</exception>
    Task<InputRunResult> RunAsync(CancellationToken cancellationToken);
}
