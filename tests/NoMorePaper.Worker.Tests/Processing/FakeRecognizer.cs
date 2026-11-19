using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Stands in for the local LLM: answers with <see cref="Answer"/> and keeps the requests.</summary>
internal sealed class FakeRecognizer : IRecognizer
{
    public static readonly RecognitionResult Empty = new(null, null, null, null, [], null, "Kurz.");

    /// <summary>What a call does; by default it returns <see cref="Answer"/>.</summary>
    public Func<RecognitionRequest, CancellationToken, Task<RecognitionResult>>? Behavior { get; set; }

    public RecognitionResult Answer { get; set; } = Empty;

    public List<RecognitionRequest> Requests { get; } = [];

    /// <summary>Completed when a call has started.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>How many calls of <see cref="PrepareAsync"/> fail before one succeeds.</summary>
    public int PrepareFailures { get; set; }

    public int PrepareCalls { get; private set; }

    public Task PrepareAsync(CancellationToken cancellationToken) =>
        ++PrepareCalls <= PrepareFailures
            ? Task.FromException(new RecognitionException("LLM service not reachable"))
            : Task.CompletedTask;

    public Task<RecognitionResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Started.TrySetResult();
        return Behavior?.Invoke(request, cancellationToken) ?? Task.FromResult(Answer);
    }

    public static Func<RecognitionRequest, CancellationToken, Task<RecognitionResult>> Throws(Exception exception) =>
        (_, _) => Task.FromException<RecognitionResult>(exception);

    /// <summary>Runs until cancelled, like a long answer during shutdown.</summary>
    public static async Task<RecognitionResult> WaitForCancellationAsync(RecognitionRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Not reached.");
    }
}
