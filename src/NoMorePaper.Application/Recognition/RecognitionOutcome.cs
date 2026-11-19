namespace NoMorePaper.Application.Recognition;

/// <summary>What one <see cref="RecognitionAttempt"/> did.</summary>
/// <param name="Commit">How the stage ended.</param>
/// <param name="SkipReason">Why the model was not asked (<c>confirmed</c>, <c>no-owner</c>, <c>no-text</c>); <c>null</c> when it was.</param>
/// <param name="ExampleCount">Examples in the request.</param>
/// <param name="RejectedCount">Values dropped by <see cref="RecognitionChecks"/>.</param>
internal sealed record RecognitionOutcome(RecognitionCommit Commit, string? SkipReason, int ExampleCount = 0, int RejectedCount = 0);
