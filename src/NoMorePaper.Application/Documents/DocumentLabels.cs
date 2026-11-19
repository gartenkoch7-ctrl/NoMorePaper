using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// The fields shown in the document list and the search hits. The entries come from a document the
/// viewer may see, so the viewer may see them too (visibility rule of the lists).
/// </summary>
/// <param name="DocumentDate">Date printed on the document.</param>
/// <param name="Recognition">Where the fields come from.</param>
public sealed record DocumentLabels(
    EntrySummary? Correspondent,
    EntrySummary? DocumentType,
    DateOnly? DocumentDate,
    Money? Amount,
    RecognitionState Recognition)
{
    /// <summary>A document without fields.</summary>
    public static DocumentLabels None { get; } = new(null, null, null, null, RecognitionState.None);
}
