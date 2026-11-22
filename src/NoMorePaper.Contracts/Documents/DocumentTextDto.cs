namespace NoMorePaper.Contracts.Documents;

/// <summary>The text the OCR recognised in a document, for the viewer tab "Recognised text".</summary>
/// <param name="Text"><c>null</c> while the document is not fully processed; empty when the OCR found nothing.</param>
public sealed record DocumentTextDto(string? Text);
