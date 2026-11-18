namespace NoMorePaper.Contracts.Documents;

/// <summary>Answer to an accepted upload (HTTP 202): the new document, queued for processing.</summary>
public sealed record UploadResultDto(Guid Id);
