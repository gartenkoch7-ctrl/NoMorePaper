using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>A stored file opened for reading; the caller disposes <paramref name="Content"/>.</summary>
/// <param name="File">Name, media type, size and hash of the file.</param>
/// <param name="Content">The bytes exactly as stored.</param>
/// <param name="Settled">
/// <c>false</c> while a stored edit still moves its files: <paramref name="File"/> may name the new version
/// while <paramref name="Content"/> is the old one, so its hash must not label these bytes (e.g. as ETag).
/// </param>
public sealed record DocumentFileContent(DocumentFile File, Stream Content, bool Settled = true);
