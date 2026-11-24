using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Editing;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Page images of planned edits below the data root: <c>edits/&lt;id&gt;/&lt;editId&gt;/page-&lt;n&gt;.jpg</c>. A write
/// goes to a temporary file that is flushed and renamed into place, so the Worker never reads a partial image.
/// </summary>
public sealed class EditPageStorage : IEditPageStorage
{
    /// <summary>Highest page number accepted; no scan has more pages.</summary>
    public const int MaxPage = 9999;

    private readonly DocumentPaths _paths;
    private readonly IDirectorySync _directories;
    private readonly ILogger<EditPageStorage> _logger;

    /// <param name="rootPath">Data root shared by Server and Worker.</param>
    /// <param name="directories">Makes each rename durable.</param>
    /// <param name="logger">Logger of the storage.</param>
    public EditPageStorage(string rootPath, IDirectorySync directories, ILogger<EditPageStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(directories);
        _paths = new DocumentPaths(rootPath);
        _directories = directories;
        _logger = logger;
    }

    public async Task SaveAsync(Guid id, Guid editId, int page, Stream jpeg, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        var target = PagePath(id, editId, page);
        var folder = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        try
        {
            long size;
            var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await using (file.ConfigureAwait(false))
            {
                await jpeg.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                file.Flush(flushToDisk: true);
                size = file.Length;
            }

            File.Move(temporary, target, overwrite: true);
            _directories.Flush(folder);
            _logger.EditPageStored(page, id, size);
        }
        catch (Exception exception)
        {
            _logger.EditPageStoreFailed(page, id, exception.GetType().Name);
            File.Delete(temporary);
            throw;
        }
    }

    public Stream OpenRead(Guid id, Guid editId, int page)
    {
        var path = PagePath(id, editId, page);
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException($"No image is stored for page {page} of document {id}.");
        }
    }

    public void Discard(Guid id, Guid editId)
    {
        var folder = _paths.EditFolder(id, editId);
        if (!Directory.Exists(folder))
        {
            return;
        }

        Directory.Delete(folder, recursive: true);
        _logger.EditPagesDiscarded(id);

        // The document's folder goes too once no other edit uses it; a new request may have added one meanwhile.
        try
        {
            Directory.Delete(_paths.EditsFolder(id));
        }
        catch (IOException)
        {
        }
    }

    private string PagePath(Guid id, Guid editId, int page)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, MaxPage);
        return Path.Combine(_paths.EditFolder(id, editId), string.Create(CultureInfo.InvariantCulture, $"page-{page}.jpg"));
    }
}
