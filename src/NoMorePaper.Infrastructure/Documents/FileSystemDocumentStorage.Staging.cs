using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

// The files an edit makes (M4b): written to edits/<id>/<editId>/ first and moved into the archive only
// after the database commit, so a failed commit leaves the files in use and their hashes consistent. The
// edits folder lies on the same volume as the archive, so every move is a rename.
public sealed partial class FileSystemDocumentStorage
{
    private const string EditsArea = "edits";

    public Task<StoredFile> StageAsync(Guid id, Guid editId, DocumentFileKind kind, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        return _writer.WriteAsync(id, kind, EditsArea, StagedPath(id, editId, kind), content, cancellationToken);
    }

    public bool IsStaged(Guid id, Guid editId, DocumentFileKind kind) => File.Exists(StagedPath(id, editId, kind));

    public Task PublishStagedAsync(Guid id, Guid editId, DocumentFileKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var staged = StagedPath(id, editId, kind);
        if (!File.Exists(staged))
        {
            return Task.CompletedTask;
        }

        var archive = _paths.ArchiveFolder(id);
        Directory.CreateDirectory(archive);
        File.Move(staged, Path.Combine(archive, Path.GetFileName(staged)), overwrite: true);
        _directories.Flush(archive);
        _logger.StagedPublished(kind, id);
        return Task.CompletedTask;
    }

    public Task TrashStagedAsync(Guid id, Guid editId, Guid trashItemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var staged = StagedPath(id, editId, DocumentFileKind.Edited);
        if (!File.Exists(staged))
        {
            return Task.CompletedTask;
        }

        var trash = _paths.TrashFolder(trashItemId);
        Directory.CreateDirectory(trash);
        File.Move(staged, Path.Combine(trash, DocumentPaths.EditedFileName), overwrite: true);
        _directories.Flush(trash);
        _logger.MovedToTrash(DocumentFileKind.Edited, id, trashItemId);
        return Task.CompletedTask;
    }

    // Same names as in the archive, so publishing is a plain rename.
    private string StagedPath(Guid id, Guid editId, DocumentFileKind kind) => Path.Combine(
        _paths.EditFolder(id, editId),
        kind switch
        {
            DocumentFileKind.Edited => DocumentPaths.EditedFileName,
            DocumentFileKind.Archive => DocumentPaths.ArchiveFileName,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only edited versions and archives are staged."),
        });
}
