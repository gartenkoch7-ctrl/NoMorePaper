using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>
/// The scanner folder (ADR-0024, docs/kb/eingaenge.md): every 30 s the finished files of the root and of every
/// account's subfolder are taken in. The root's files belong to nobody known (administrator, private, owner
/// check), a subfolder's to its account. Only files directly in those folders count; hidden files, Office
/// lock files (<c>~$</c>) and deeper folders are left alone.
/// </summary>
public sealed class ScannerFolderSource(
    ScannerFolderSettings settings,
    AccountFolders folders,
    StableFiles stable,
    ScannerFileIntake files,
    ILogger<ScannerFolderSource> logger) : IInputSource
{
    public static readonly TimeSpan RunInterval = TimeSpan.FromSeconds(30);

    public InputKind Kind => InputKind.ScannerFolder;

    public TimeSpan Interval => RunInterval;

    public async Task<InputRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var root = settings.Path;
        ScannerFolderPlan plan;
        try
        {
            if (!Directory.Exists(root))
            {
                logger.Unreachable("missing");
                return InputRunResult.Failed(new InputProblem(InputProblemKind.Unreachable, NeedsAttention: true));
            }

            plan = await folders.PrepareAsync(root, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Unreachable(exception.GetType().Name);
            return InputRunResult.Failed(new InputProblem(InputProblemKind.Unreachable, NeedsAttention: true));
        }

        var errorFolder = Path.Combine(root, AccountFolders.ErrorFolderName);
        var present = new List<string>();
        var (finished, accepted, skipped, failures) = (0, 0, 0, plan.Failures);
        foreach (var (folder, ownerId) in plan.Folders)
        {
            foreach (var path in ListFiles(folder, ref failures))
            {
                cancellationToken.ThrowIfCancellationRequested();
                present.Add(path);
                if (!stable.IsFinished(path))
                {
                    continue;
                }

                finished++;
                var outcome = await files.TakeAsync(path, ownerId, errorFolder, cancellationToken).ConfigureAwait(false);
                if (outcome is not (ScannerFileOutcome.Kept or ScannerFileOutcome.Failed))
                {
                    stable.Forget(path);
                }

                switch (outcome)
                {
                    case ScannerFileOutcome.Accepted:
                        accepted++;
                        break;
                    case ScannerFileOutcome.Duplicate or ScannerFileOutcome.MovedToErrors:
                        skipped++;
                        break;
                    case ScannerFileOutcome.Failed:
                        failures++;
                        break;
                    default:
                        break;
                }
            }
        }

        stable.KeepOnly(present);
        logger.Ran(finished, accepted);
        return new InputRunResult(accepted, skipped, Problem(failures, plan.UnknownFolders));
    }

    /// <summary>Whether a file in the scanner folder is not for the intake: hidden (also macOS' <c>._</c> files) or an Office lock file.</summary>
    public static bool IsIgnored(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.StartsWith('.') || name.StartsWith("~$", StringComparison.Ordinal);
    }

    // A failed file (database away) outweighs unknown folders: it is the more urgent message, and the
    // unknown folders are reported again by the next run that gets through.
    private static InputProblem? Problem(int failures, int unknownFolders) =>
        failures > 0 ? new InputProblem(InputProblemKind.Failed)
        : unknownFolders > 0 ? new InputProblem(InputProblemKind.UnknownFolder, NeedsAttention: true)
        : null;

    private List<string> ListFiles(string folder, ref int failures)
    {
        try
        {
            return [.. Directory.EnumerateFiles(folder).Where(path => !IsIgnored(Path.GetFileName(path))).Order(StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // E.g. a subfolder deleted over SMB right now; the others still run.
            logger.Unreachable(exception.GetType().Name);
            failures++;
            return [];
        }
    }
}
