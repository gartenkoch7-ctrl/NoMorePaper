namespace NoMorePaper.Contracts.Documents;

/// <summary>Answer to an accepted upload (HTTP 202): the new document, queued for processing.</summary>
/// <param name="IsPrivate">
/// Whether the document is private as the server applied it: the uploader's choice, or without one the
/// account's setting at that moment. The page says this, not what it expected.
/// </param>
public sealed record UploadResultDto(Guid Id, bool IsPrivate);
