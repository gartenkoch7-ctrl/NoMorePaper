using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Stores document files below a data root (the <c>/data</c> volume shared by Server and Worker).
/// Writes go to a temporary file in the target folder and are renamed into place, which is atomic
/// on one file system, and the target folder is flushed after each rename (<see cref="DirectorySync"/>);
/// readers open files so that a concurrent rename or replace does not fail.
/// </summary>
public sealed class FileSystemDocumentStorage : IDocumentStorage
{
    private const string InboxArea = "inbox";
    private const string ArchiveArea = "archive";
    private const string KindTag = "nomorepaper.storage.kind";
    private const int BufferSize = 81920;

    private static readonly Counter<long> s_storedBytes = InfrastructureTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.storage.stored_bytes", unit: "By", description: "Bytes written to the document storage");

    private readonly DocumentPaths _paths;
    private readonly DirectorySync _directories = new();
    private readonly ILogger<FileSystemDocumentStorage> _logger;

    /// <param name="rootPath">Data root; created on the first write.</param>
    public FileSystemDocumentStorage(string rootPath, ILogger<FileSystemDocumentStorage> logger)
    {
        _paths = new DocumentPaths(rootPath);
        _logger = logger;
    }

    public async Task<StoredFile> SaveInboxAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var folder = _paths.InboxFolder(id);
        var target = Path.Combine(folder, DocumentPaths.OriginalFileName(fileName));

        var stored = await WriteAtomicallyAsync(id, DocumentFileKind.Original, InboxArea, target, content, cancellationToken)
            .ConfigureAwait(false);
        RemoveOtherOriginals(id, InboxArea, target);
        return stored;
    }

    public Task<StoredFile> SaveArchiveAsync(Guid id, DocumentFileKind kind, Stream content, CancellationToken cancellationToken)
    {
        if (kind != DocumentFileKind.Archive)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Originals reach the archive through ArchiveOriginalAsync.");
        }

        ArgumentNullException.ThrowIfNull(content);
        var target = Path.Combine(_paths.ArchiveFolder(id), DocumentPaths.ArchiveFileName);
        return WriteAtomicallyAsync(id, kind, ArchiveArea, target, content, cancellationToken);
    }

    public Task ArchiveOriginalAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.storage.archive_original");
        var inbox = _paths.InboxFolder(id);
        var archive = _paths.ArchiveFolder(id);

        var source = FindOriginal(inbox);
        if (source is null)
        {
            // A retried job finds the original already moved.
            return FindOriginal(archive) is not null
                ? Task.CompletedTask
                : throw new FileNotFoundException($"No original is stored for document {id}.");
        }

        Directory.CreateDirectory(archive);
        var target = Path.Combine(archive, Path.GetFileName(source));
        File.Move(source, target, overwrite: true);
        _directories.Flush(archive);
        _logger.OriginalArchived(id);

        RemoveOtherOriginals(id, ArchiveArea, target);
        TryCleanUp(id, InboxArea, () => Directory.Delete(inbox, recursive: true));
        return Task.CompletedTask;
    }

    public Stream OpenRead(Guid id, DocumentFileKind kind)
    {
        var archive = _paths.ArchiveFolder(id);
        var stream = kind switch
        {
            DocumentFileKind.Archive => TryOpen(Path.Combine(archive, DocumentPaths.ArchiveFileName)),

            // Archive, then inbox, then archive again: the original may move in between.
            DocumentFileKind.Original => TryOpen(FindOriginal(archive))
                ?? TryOpen(FindOriginal(_paths.InboxFolder(id)))
                ?? TryOpen(FindOriginal(archive)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown file kind."),
        };

        return stream ?? throw new FileNotFoundException($"No {kind} file is stored for document {id}.");
    }

    private async Task<StoredFile> WriteAtomicallyAsync(
        Guid id, DocumentFileKind kind, string area, string target, Stream content, CancellationToken cancellationToken)
    {
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.storage.write");
        activity?.SetTag(KindTag, kind.ToString());

        var folder = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        try
        {
            StoredFile stored;
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            var file = new FileStream(temporary, options);
            await using (file.ConfigureAwait(false))
            {
                stored = await CopyAndHashAsync(content, file, cancellationToken).ConfigureAwait(false);

                // Durable before the rename publishes it; otherwise a power loss could leave an empty file.
                file.Flush(flushToDisk: true);
            }

            File.Move(temporary, target, overwrite: true);

            // Only the folder entry makes the new name survive a power loss (Linux).
            _directories.Flush(folder);
            s_storedBytes.Add(stored.SizeBytes, new KeyValuePair<string, object?>(KindTag, kind.ToString()));
            _logger.Stored(kind, id, area, stored.SizeBytes);
            return stored;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            TryCleanUp(id, area, () => File.Delete(temporary));
            if (exception is not OperationCanceledException)
            {
                _logger.StoreFailed(kind, id, exception.GetType().Name);
            }

            throw;
        }
    }

    private static async Task<StoredFile> CopyAndHashAsync(Stream source, Stream target, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long size = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                size += read;
            }

            return new StoredFile(size, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string? FindOriginal(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, DocumentPaths.OriginalPattern).FirstOrDefault();
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    // FileShare.Delete lets a writer replace or move the file while it is being read.
    private static FileStream? TryOpen(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    // An earlier original with another extension would make the original ambiguous.
    private void RemoveOtherOriginals(Guid id, string area, string keep)
    {
        var folder = Path.GetDirectoryName(keep)!;
        foreach (var other in Directory.EnumerateFiles(folder, DocumentPaths.OriginalPattern))
        {
            if (!string.Equals(other, keep, StringComparison.Ordinal))
            {
                TryCleanUp(id, area, () => File.Delete(other));
            }
        }
    }

    private void TryCleanUp(Guid id, string area, Action cleanUp)
    {
        try
        {
            cleanUp();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The stored state is already correct; a leftover only costs space.
            _logger.CleanupFailed(id, area, exception.GetType().Name);
        }
    }
}
