namespace NoMorePaper.Application.Recognition;

/// <summary>What <see cref="IRecognizer"/> works with; only lists and examples the document's owner may see.</summary>
/// <param name="Text">OCR text of the document, possibly shortened by the caller.</param>
/// <param name="Correspondents">Names of existing correspondents, preferred over new ones.</param>
/// <param name="DocumentTypes">Names of existing document types, preferred over new ones.</param>
/// <param name="Tags">Names of existing tags, preferred over new ones.</param>
/// <param name="Examples">Confirmed documents of the same correspondent, as examples of the expected answer.</param>
public sealed record RecognitionRequest(
    string Text,
    IReadOnlyList<string> Correspondents,
    IReadOnlyList<string> DocumentTypes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<RecognitionExample> Examples);
