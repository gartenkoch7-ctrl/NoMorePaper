namespace NoMorePaper.Domain.Documents;

/// <summary>Metadata of one stored file of a document; the bytes live in the document storage.</summary>
public sealed class DocumentFile
{
    /// <summary>Longest file name kept; longer names must be shortened by the caller.</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>Longest media type kept (RFC 6838 allows 127 + 1 + 127 characters).</summary>
    public const int MaxMediaTypeLength = 255;

    /// <summary>Length of a SHA-256 hash as lowercase hex.</summary>
    public const int Sha256Length = 64;

    /// <param name="kind">Original or archive file.</param>
    /// <param name="fileName">Name offered on download (for the original: the name it was received with).</param>
    /// <param name="mediaType">Media type, e.g. <c>application/pdf</c>.</param>
    /// <param name="sizeBytes">Size of the stored bytes.</param>
    /// <param name="sha256">SHA-256 of the stored bytes as 64 lowercase hex characters.</param>
    public DocumentFile(DocumentFileKind kind, string fileName, string mediaType, long sizeBytes, string sha256)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown file kind.");
        }

        RequireText(fileName, MaxFileNameLength, nameof(fileName));
        RequireText(mediaType, MaxMediaTypeLength, nameof(mediaType));
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);
        if (!IsSha256Hex(sha256))
        {
            throw new ArgumentException("Expected 64 lowercase hex characters.", nameof(sha256));
        }

        Kind = kind;
        FileName = fileName;
        MediaType = mediaType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
    }

    public DocumentFileKind Kind { get; }

    public string FileName { get; }

    public string MediaType { get; }

    public long SizeBytes { get; }

    public string Sha256 { get; }

    private static void RequireText(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"At most {maxLength} characters.", parameterName);
        }
    }

    private static bool IsSha256Hex(string? value) =>
        value is { Length: Sha256Length } && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
