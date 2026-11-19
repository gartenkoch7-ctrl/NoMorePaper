using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Worker.Tests.Inputs;

/// <summary>Keeps every recorded input state; <see cref="Failure"/> makes recording fail like an absent database.</summary>
internal sealed class RecordingStatus : IInputStatusStore
{
    private readonly List<(InputKind Kind, Guid? AccountId, int Received, InputProblem? Problem)> _recorded = [];

    public Exception? Failure { get; set; }

    /// <summary>The only state recorded for the row.</summary>
    public (int Received, InputProblem? Problem) Single(InputKind kind, Guid? accountId)
    {
        lock (_recorded)
        {
            var row = _recorded.Single(entry => entry.Kind == kind && entry.AccountId == accountId);
            return (row.Received, row.Problem);
        }
    }

    /// <summary>The latest state recorded for the row.</summary>
    public (int Received, InputProblem? Problem) Last(InputKind kind, Guid? accountId)
    {
        lock (_recorded)
        {
            var row = _recorded.Last(entry => entry.Kind == kind && entry.AccountId == accountId);
            return (row.Received, row.Problem);
        }
    }

    public Task RecordAsync(InputKind kind, Guid? accountId, int received, InputProblem? problem, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        lock (_recorded)
        {
            _recorded.Add((kind, accountId, received, problem));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InputStatus>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
