namespace NoMorePaper.Application.Recognition;

/// <summary>Result of <see cref="RecognitionChecks.Apply"/>.</summary>
/// <param name="Result">The answer with every value that failed a check dropped, names trimmed, title and summary cut.</param>
/// <param name="Rejected">Fields with a dropped value (<see cref="RecognitionChecks.Correspondent"/>, …), for the log.</param>
public sealed record CheckedRecognition(RecognitionResult Result, IReadOnlyList<string> Rejected);
