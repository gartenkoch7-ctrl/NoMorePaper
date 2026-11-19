namespace NoMorePaper.Contracts.Classification;

/// <summary>An entry of a maintained list: a correspondent, a document type or a tag.</summary>
public sealed record EntryDto(Guid Id, string Name);
