using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Contracts.Documents;

/// <summary>How many documents have a document type.</summary>
public sealed record DocumentTypeCountDto(EntryDto Type, int Count);
