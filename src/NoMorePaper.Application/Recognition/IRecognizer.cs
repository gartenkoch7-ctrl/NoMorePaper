namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Proposes the fields of a document from its text with a local LLM (ADR-0004). Runs in the own
/// network; no document content leaves it (ADR-0002).
/// </summary>
public interface IRecognizer
{
    /// <summary>Recognizes correspondent, document type, date, amount, tags, title and summary.</summary>
    /// <returns>The proposal; every field the model could not determine is empty.</returns>
    /// <exception cref="TimeoutException">Recognition took longer than its time limit.</exception>
    Task<RecognitionResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken);
}
