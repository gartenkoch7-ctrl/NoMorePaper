namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Proposes the fields of a document from its text with a local LLM (ADR-0004, ADR-0022). Runs in the
/// own network; no document content leaves it (ADR-0002).
/// </summary>
public interface IRecognizer
{
    /// <summary>Makes sure the model is available, downloading it once if it is missing.</summary>
    /// <exception cref="RecognitionException">The LLM service is not reachable or refused.</exception>
    Task PrepareAsync(CancellationToken cancellationToken);

    /// <summary>Recognizes correspondent, document type, date, amount, tags, title and summary.</summary>
    /// <returns>The proposal; every field the model could not determine is empty.</returns>
    /// <exception cref="TimeoutException">Recognition took longer than its time limit.</exception>
    /// <exception cref="RecognitionException">
    /// The LLM service is not reachable or refused, or the answer of the model cannot be used.
    /// </exception>
    Task<RecognitionResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken);
}
