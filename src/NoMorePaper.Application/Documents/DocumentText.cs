namespace NoMorePaper.Application.Documents;

/// <summary>The text the OCR recognised in a document.</summary>
/// <param name="Text"><c>null</c> while the document is not fully processed; empty when the OCR found nothing.</param>
public sealed record DocumentText(string? Text);
