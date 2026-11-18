using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>A stored file opened for reading; the caller disposes <paramref name="Content"/>.</summary>
/// <param name="File">Name, media type, size and hash of the file.</param>
/// <param name="Content">The bytes exactly as stored.</param>
public sealed record DocumentFileContent(DocumentFile File, Stream Content);
