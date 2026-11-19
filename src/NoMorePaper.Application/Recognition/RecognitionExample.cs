namespace NoMorePaper.Application.Recognition;

/// <summary>A confirmed document as an example for <see cref="IRecognizer"/>.</summary>
/// <param name="Text">OCR text of the example, possibly shortened.</param>
/// <param name="Fields">Its confirmed fields, by name.</param>
public sealed record RecognitionExample(string Text, RecognitionResult Fields);
