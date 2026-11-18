namespace NoMorePaper.Contracts.Documents;

/// <summary>Metadata of a downloadable file; the bytes come from <c>/api/documents/{id}/files/{original|archive}</c>.</summary>
public sealed record DocumentFileDto(string FileName, string MediaType, long SizeBytes, string Sha256);
