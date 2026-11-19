namespace NoMorePaper.Application.Recognition;

/// <summary>
/// A confirmed document as an example for <see cref="IRecognizer"/>: only its names and title, never
/// its text (docs/kb/erkennung.md, "Beispiele").
/// </summary>
/// <param name="Correspondent">Name of its correspondent.</param>
/// <param name="DocumentType">Name of its document type, if it has one.</param>
/// <param name="Tags">Names of its tags.</param>
/// <param name="Title">Its title.</param>
public sealed record RecognitionExample(string Correspondent, string? DocumentType, IReadOnlyList<string> Tags, string Title);
