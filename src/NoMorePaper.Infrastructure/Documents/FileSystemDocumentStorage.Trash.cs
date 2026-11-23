using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

// The newest edited version and the trash folders (M4b, ADR-0025): trash/<trashItemId>/<file>. Files
// are moved, never copied, and each move is safe to repeat when a step after it failed.
public sealed partial class FileSystemDocumentStorage
{
    private const string TrashArea = "trash";

    public Task<StoredFile> SaveEditedAsync(Guid id, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var target = Path.Combine(_paths.ArchiveFolder(id), DocumentPaths.EditedFileName);
        return _writer.WriteAsync(id, DocumentFileKind.Edited, ArchiveArea, target, content, cancellationToken);
    }

    public Task MoveToTrashAsync(Guid id, DocumentFileKind kind, Guid trashItemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archive = _paths.ArchiveFolder(id);
        var trash = _paths.TrashFolder(trashItemId);
        var source = FindInFolder(archive, kind);
        if (source is null)
        {
            // A repeated call finds the file moved already.
            return FindInFolder(trash, kind) is not null
                ? Task.CompletedTask
                : throw new FileNotFoundException($"No {kind} file is stored for document {id}.");
        }

        Directory.CreateDirectory(trash);
        File.Move(source, Path.Combine(trash, Path.GetFileName(source)));
        _directories.Flush(trash);
        _directories.Flush(archive);
        _logger.MovedToTrash(kind, id, trashItemId);
        return Task.CompletedTask;
    }

    public Task RestoreFromTrashAsync(Guid trashItemId, Guid id, DocumentFileKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archive = _paths.ArchiveFolder(id);
        var trash = _paths.TrashFolder(trashItemId);
        var source = FindInFolder(trash, kind);
        if (source is null)
        {
            return FindInFolder(archive, kind) is not null
                ? Task.CompletedTask
                : throw new FileNotFoundException($"No {kind} file of document {id} is in trash entry {trashItemId}.");
        }

        if (FindInFolder(archive, kind) is not null)
        {
            throw new IOException($"Document {id} has a {kind} file already.");
        }

        Directory.CreateDirectory(archive);
        File.Move(source, Path.Combine(archive, Path.GetFileName(source)));
        _directories.Flush(archive);
        _logger.RestoredFromTrash(kind, id, trashItemId);
        _cleanup.TryCleanUp(id, TrashArea, () => Directory.Delete(trash, recursive: true));
        return Task.CompletedTask;
    }

    public void PurgeTrash(Guid trashItemId)
    {
        var trash = _paths.TrashFolder(trashItemId);
        if (Directory.Exists(trash))
        {
            Directory.Delete(trash, recursive: true);
            _logger.TrashPurged(trashItemId);
        }
    }

    // The original keeps its received extension; the edited version has a fixed name. The archive is
    // always made anew and never goes to the trash.
    private static string? FindInFolder(string folder, DocumentFileKind kind) => kind switch
    {
        DocumentFileKind.Original => FindOriginal(folder),
        DocumentFileKind.Edited => File.Exists(Path.Combine(folder, DocumentPaths.EditedFileName))
            ? Path.Combine(folder, DocumentPaths.EditedFileName)
            : null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only originals and edited versions go to the trash."),
    };
}
