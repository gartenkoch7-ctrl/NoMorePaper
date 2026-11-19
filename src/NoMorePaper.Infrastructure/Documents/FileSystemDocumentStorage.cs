using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Stores document files below a data root (the <c>/data</c> volume shared by Server and Worker).
/// Writes are atomic (<see cref="AtomicFileWriter"/>), moves flush the target folder
/// (<see cref="IDirectorySync"/>), and readers open files so that a concurrent rename or replace
/// does not fail.
/// </summary>
public sealed class FileSystemDocumentStorage : IDocumentStorage
{
    private const string InboxArea = "inbox";
    private const string ArchiveArea = "archive";

    private readonly DocumentPaths _paths;
    private readonly IDirectorySync _directories;
    private readonly ILogger<FileSystemDocumentStorage> _logger;
    private readonly StorageCleanup _cleanup;
    private readonly AtomicFileWriter _writer;

    /// <param name="rootPath">Data root; created on the first write.</param>
    /// <param name="directories">Makes each rename durable.</param>
    /// <param name="logger">Logger of the storage.</param>
    public FileSystemDocumentStorage(string rootPath, IDirectorySync directories, ILogger<FileSystemDocumentStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(directories);
        _paths = new DocumentPaths(rootPath);
        _directories = directories;
        _logger = logger;
        _cleanup = new StorageCleanup(logger);
        _writer = new AtomicFileWriter(directories, _cleanup, logger);
    }

    public async Task<StoredFile> SaveInboxAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var folder = _paths.InboxFolder(id);
        var target = Path.Combine(folder, DocumentPaths.OriginalFileName(fileName));

        var stored = await _writer.WriteAsync(id, DocumentFileKind.Original, InboxArea, target, content, cancellationToken)
            .ConfigureAwait(false);
        _cleanup.RemoveOtherOriginals(id, InboxArea, target);
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
        return _writer.WriteAsync(id, kind, ArchiveArea, target, content, cancellationToken);
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

        _cleanup.RemoveOtherOriginals(id, ArchiveArea, target);
        _cleanup.TryCleanUp(id, InboxArea, () => Directory.Delete(inbox, recursive: true));
        return Task.CompletedTask;
    }

    public void DiscardInbox(Guid id)
    {
        var folder = _paths.InboxFolder(id);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    public void Delete(Guid id)
    {
        foreach (var folder in new[] { _paths.InboxFolder(id), _paths.ArchiveFolder(id) })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
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
}
