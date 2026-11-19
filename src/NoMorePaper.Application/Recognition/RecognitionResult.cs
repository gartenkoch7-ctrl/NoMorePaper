using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Fields proposed by <see cref="IRecognizer"/>; any of them may be empty. Correspondent, document type
/// and tags are names, matched to list entries by the caller.
/// </summary>
/// <param name="Correspondent">Who sent the document.</param>
/// <param name="DocumentType">What kind of document it is.</param>
/// <param name="DocumentDate">Date printed on the document.</param>
/// <param name="Amount">Amount with its currency.</param>
/// <param name="Tags">Keywords; empty when there are none.</param>
/// <param name="Title">A short title that replaces the file name.</param>
/// <param name="Summary">One or two sentences about the content.</param>
public sealed record RecognitionResult(
    string? Correspondent,
    string? DocumentType,
    DateOnly? DocumentDate,
    Money? Amount,
    IReadOnlyList<string> Tags,
    string? Title,
    string? Summary);
