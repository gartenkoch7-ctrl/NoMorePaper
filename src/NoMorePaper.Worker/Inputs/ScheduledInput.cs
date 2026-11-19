using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Worker.Inputs;

/// <summary>
/// When an input is due next and whether a run of it is going on. The scheduler's timer and the run
/// itself both touch it, hence the lock.
/// </summary>
internal sealed class ScheduledInput(InputKind kind, DateTimeOffset dueAt)
{
    private readonly Lock _lock = new();
    private DateTimeOffset _dueAt = dueAt;
    private Task? _running;

    public InputKind Kind { get; } = kind;

    /// <summary>The current run, or the last one; completed when none is going on.</summary>
    public Task Running
    {
        get
        {
            lock (_lock)
            {
                return _running ?? Task.CompletedTask;
            }
        }
    }

    /// <summary>Starts a run if the input is due at <paramref name="now"/> and not running.</summary>
    /// <param name="provisionalDueAt">Next start until the run knows its source's interval (e.g. the source cannot be built).</param>
    /// <returns>The started run, or <c>null</c>.</returns>
    public Task? TryStart(DateTimeOffset now, DateTimeOffset provisionalDueAt, Func<Task> run)
    {
        lock (_lock)
        {
            if (_dueAt > now || _running is { IsCompleted: false })
            {
                return null;
            }

            _dueAt = provisionalDueAt;
            _running = Task.Run(run, CancellationToken.None);
            return _running;
        }
    }

    /// <summary>Sets the next start, once the run knows its source's interval.</summary>
    public void Reschedule(DateTimeOffset dueAt)
    {
        lock (_lock)
        {
            _dueAt = dueAt;
        }
    }
}
