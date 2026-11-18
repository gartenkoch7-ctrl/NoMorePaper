using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Turns a name supplied by an uploader into something safe to store and show: no directories, no
/// control characters (Postgres rejects NUL), within the length limits of the model. Pure logic.
/// </summary>
public static class DocumentNames
{
    private const string FallbackName = "document";
    private const int MaxExtensionLength = 32;

    /// <summary>The last path segment (either separator style), cleaned and shortened to fit <see cref="DocumentFile.MaxFileNameLength"/>.</summary>
    public static string FileName(string? received)
    {
        var name = Clean(LastSegment(received));
        if (name.Length == 0)
        {
            return FallbackName;
        }

        if (name.Length <= DocumentFile.MaxFileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        if (extension.Length > MaxExtensionLength)
        {
            extension = string.Empty;
        }

        return name[..(DocumentFile.MaxFileNameLength - extension.Length)] + extension;
    }

    /// <summary>The file name without its extension, at most <see cref="Document.MaxTitleLength"/> characters.</summary>
    public static string Title(string? received)
    {
        var stem = Path.GetFileNameWithoutExtension(FileName(received)).Trim();
        if (stem.Length == 0)
        {
            return FallbackName;
        }

        return stem.Length > Document.MaxTitleLength ? stem[..Document.MaxTitleLength] : stem;
    }

    private static string LastSegment(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var cut = name.LastIndexOfAny(['/', '\\']);
        return cut < 0 ? name : name[(cut + 1)..];
    }

    private static string Clean(string name) => string.Concat(name.Where(c => !char.IsControl(c))).Trim();
}
