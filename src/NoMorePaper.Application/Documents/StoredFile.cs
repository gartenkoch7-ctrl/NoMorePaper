namespace NoMorePaper.Application.Documents;

/// <summary>What the storage wrote: size and SHA-256 (64 lowercase hex characters) of the bytes.</summary>
public sealed record StoredFile(long SizeBytes, string Sha256);
