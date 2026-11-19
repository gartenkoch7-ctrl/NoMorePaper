using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Worker.Tests.Inputs;

/// <summary>An input that counts its runs and answers with <see cref="Behavior"/>, by default one document.</summary>
internal sealed class FakeInputSource(InputKind kind, TimeSpan interval) : IInputSource
{
    private int _runs;

    public Func<CancellationToken, Task<InputRunResult>>? Behavior { get; set; }

    public int Runs => _runs;

    /// <summary>Completed when the first run starts.</summary>
    public TaskCompletionSource FirstRun { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public InputKind Kind => kind;

    public TimeSpan Interval => interval;

    public Task<InputRunResult> RunAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _runs);
        FirstRun.TrySetResult();
        return Behavior?.Invoke(cancellationToken) ?? Task.FromResult(new InputRunResult(1, 0));
    }
}
