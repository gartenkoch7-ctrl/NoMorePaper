using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>
/// Takes one finished file of the scanner folder in: recognizes its type by its first bytes, hands it to
/// <see cref="IntakeDocument"/> for its folder's owner and deletes it only once the intake answered
/// <see cref="IntakeStatus.Received"/> or <see cref="IntakeStatus.AlreadyThere"/>, i.e. after the commit.
/// Anything else leaves the file where it is; a file of unknown type goes to the error folder.
/// </summary>
public sealed class ScannerFileIntake(IntakeDocument intake, TimeProvider clock, ILogger<ScannerFileIntake> logger)
{
    // Linux allows 255 bytes per name; the time prefix and a counter need the rest.
    private const int MaxErrorNameBytes = 220;
    private const int MaxMoveAttempts = 20;

    /// <param name="ownerId">The folder's account, or <c>null</c> for the root (unknown owner).</param>
    /// <param name="errorFolder">Where files of unknown type go.</param>
    /// <exception cref="OperationCanceledException">The Worker stops; the file stays.</exception>
    public async Task<ScannerFileOutcome> TakeAsync(string path, Guid? ownerId, string errorFolder, CancellationToken cancellationToken)
    {
        long length;
        DateTime lastWrite;
        IntakeResult? result = null;
        try
        {
            var before = new FileInfo(path);
            (length, lastWrite) = (before.Length, before.LastWriteTimeUtc);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                var head = new byte[FileSignature.HeadLength];
                var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (FileSignature.DetectMediaType(head.AsSpan(0, read)) is { } mediaType)
                {
                    stream.Position = 0;
                    var request = new IntakeRequest(ownerId, DocumentSource.ScannerFolder, Path.GetFileName(path), mediaType, stream);
                    result = await intake.AcceptAsync(request, isPrivate: null, DuplicateScope.Owned, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Database away, disk full, file locked: nothing is lost, the next run tries again.
            logger.IntakeFailed(exception.GetType().Name);
            return ScannerFileOutcome.Failed;
        }

        if (result is null)
        {
            return MoveToErrors(path, errorFolder);
        }

        switch (result.Status)
        {
            case IntakeStatus.Received:
                Delete(path, length, lastWrite);
                return ScannerFileOutcome.Accepted;
            case IntakeStatus.AlreadyThere:
                Delete(path, length, lastWrite);
                return ScannerFileOutcome.Duplicate;
            default:
                // Owner removed meanwhile, or no administrator yet: the intake logged it (5301, 5302).
                return ScannerFileOutcome.Kept;
        }
    }

    /// <summary>The name in the error folder: time of the move, a counter on clashes, then the file's own name, shortened if too long.</summary>
    public static string ErrorName(string name, DateTimeOffset at, int attempt)
    {
        ArgumentNullException.ThrowIfNull(name);
        var prefix = at.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + (attempt > 0 ? "-" + attempt.ToString(CultureInfo.InvariantCulture) : string.Empty) + "-";
        var extension = Path.GetExtension(name);
        if (extension.Length > 16)
        {
            extension = string.Empty;
        }

        var stem = name[..^extension.Length];
        while (stem.Length > 0 && Encoding.UTF8.GetByteCount(stem + extension) > MaxErrorNameBytes)
        {
            // Never cut a surrogate pair in half.
            stem = stem[..^(stem.Length > 1 && char.IsLowSurrogate(stem[^1]) ? 2 : 1)];
        }

        return prefix + stem + extension;
    }

    // Deleted only when it is still the file that was read: a scanner may have written a new one under its name.
    private void Delete(string path, long length, DateTime lastWrite)
    {
        try
        {
            var now = new FileInfo(path);
            if (!now.Exists)
            {
                return;
            }

            if (now.Length != length || now.LastWriteTimeUtc != lastWrite)
            {
                logger.ChangedDuringIntake();
                return;
            }

            now.Delete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.DeleteFailed(exception.GetType().Name);
        }
    }

    private ScannerFileOutcome MoveToErrors(string path, string errorFolder)
    {
        var name = Path.GetFileName(path);
        try
        {
            Directory.CreateDirectory(errorFolder);
            var at = clock.GetUtcNow();
            for (var attempt = 0; attempt < MaxMoveAttempts; attempt++)
            {
                var target = Path.Combine(errorFolder, ErrorName(name, at, attempt));
                if (File.Exists(target))
                {
                    continue;
                }

                File.Move(path, target, overwrite: false);
                logger.MovedToErrors("unknown-type");
                return ScannerFileOutcome.MovedToErrors;
            }

            logger.MoveFailed("NameTaken");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.MoveFailed(exception.GetType().Name);
        }

        return ScannerFileOutcome.Failed;
    }
}
