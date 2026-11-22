using NoMorePaper.Application.Classification;

namespace NoMorePaper.Application.Documents;

/// <summary>How many documents have a document type.</summary>
public sealed record DocumentTypeCount(EntrySummary Type, int Count);
