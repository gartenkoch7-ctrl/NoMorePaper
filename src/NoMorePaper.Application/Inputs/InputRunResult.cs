namespace NoMorePaper.Application.Inputs;

/// <summary>What one run of an <see cref="IInputSource"/> did.</summary>
/// <param name="Received">Documents taken in by the whole input, all accounts together.</param>
/// <param name="Skipped">Items left out on purpose (e.g. a file the owner has already, spam).</param>
/// <param name="Problem">Why the run as a whole did not get through, or <c>null</c> when it did.</param>
public sealed record InputRunResult(int Received, int Skipped, InputProblem? Problem = null)
{
    /// <summary>A run without anything new and without problem.</summary>
    public static InputRunResult Nothing { get; } = new(0, 0);

    /// <summary>
    /// The results of the accounts' parts, for inputs that run per account (iCloud); empty for inputs that
    /// serve the whole household at once (scanner folder, mailbox). Each is recorded under its account.
    /// </summary>
    public IReadOnlyList<InputAccountResult> Accounts { get; init; } = [];

    /// <summary>A run that ended at a problem before taking anything in.</summary>
    public static InputRunResult Failed(InputProblem problem) => new(0, 0, problem);
}
