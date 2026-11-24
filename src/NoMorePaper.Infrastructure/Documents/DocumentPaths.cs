using System.Globalization;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Folder layout below the data root (docs/plan/m1-durchstich.md, task 1):
/// <c>inbox/&lt;id&gt;/original.&lt;ext&gt;</c> and <c>archive/&lt;yyyy&gt;/&lt;mm&gt;/&lt;id&gt;/original.&lt;ext&gt;</c> plus
/// <c>archive.pdf</c>. Every segment is built from the id, a fixed name or a cleaned extension, so
/// no caller-supplied text can leave the root.
/// </summary>
internal sealed class DocumentPaths
{
    public const string ArchiveFileName = "archive.pdf";

    /// <summary>The newest edited version, next to the archive PDF (M4b).</summary>
    public const string EditedFileName = "edited.pdf";

    /// <summary>Search pattern for the original, whatever its extension.</summary>
    public const string OriginalPattern = OriginalBaseName + ".*";

    private const string OriginalBaseName = "original";
    private const string FallbackExtension = "bin";
    private const int MaxExtensionLength = 10;

    private readonly string _root;
    private readonly string _rootPrefix;

    public DocumentPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _rootPrefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
    }

    public string InboxFolder(Guid id) => Below("inbox", IdSegment(id));

    /// <summary><c>trash/&lt;trashItemId&gt;/</c>: the files of one trash entry (M4b).</summary>
    public string TrashFolder(Guid trashItemId) => Below("trash", IdSegment(trashItemId));

    /// <summary><c>edits/&lt;id&gt;/</c>: page images of an edit the Worker has not carried out yet (M4b).</summary>
    public string EditsFolder(Guid id) => Below("edits", IdSegment(id));

    /// <summary><c>edits/&lt;id&gt;/&lt;editId&gt;/</c>: page images and staged files of one edit (M4b).</summary>
    public string EditFolder(Guid id, Guid editId) => Below("edits", IdSegment(id), IdSegment(editId));

    /// <summary>Year and month come from the timestamp inside the version 7 id (UTC), so they never change.</summary>
    public string ArchiveFolder(Guid id)
    {
        var segment = IdSegment(id);
        var created = CreatedAt(id);
        return Below(
            "archive",
            created.Year.ToString("D4", CultureInfo.InvariantCulture),
            created.Month.ToString("D2", CultureInfo.InvariantCulture),
            segment);
    }

    /// <summary><c>original.&lt;ext&gt;</c> with the received extension, lowercased; <c>bin</c> if it is unusable.</summary>
    public static string OriginalFileName(string receivedFileName)
    {
        ArgumentNullException.ThrowIfNull(receivedFileName);
        var extension = Path.GetExtension(receivedFileName).TrimStart('.').ToLowerInvariant();
        var usable = extension.Length is > 0 and <= MaxExtensionLength && extension.All(char.IsAsciiLetterOrDigit);
        return $"{OriginalBaseName}.{(usable ? extension : FallbackExtension)}";
    }

    private string Below(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine([_root, .. segments]));

        // Defense in depth: the segments above cannot escape, but a future change might.
        if (!path.StartsWith(_rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage path left the data root.");
        }

        return path;
    }

    private static string IdSegment(Guid id) =>
        id.Version == 7 ? id.ToString("D") : throw new ArgumentException("Document ids are version 7 GUIDs.", nameof(id));

    // RFC 9562: the first 48 bits of a version 7 UUID are the Unix time in milliseconds.
    private static DateTimeOffset CreatedAt(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);
        long milliseconds = 0;
        foreach (var b in bytes[..6])
        {
            milliseconds = (milliseconds << 8) | b;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }
}
