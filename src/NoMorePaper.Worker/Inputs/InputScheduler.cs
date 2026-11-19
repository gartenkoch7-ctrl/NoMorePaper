using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Worker.Inputs;

/// <summary>
/// Runs every registered background input in its interval (ADR-0024). Learns the inputs from their
/// <see cref="InputSourceRegistration"/>s, without building a source; checks every <see cref="CheckInterval"/>
/// which inputs are due and runs each in a scope of its own, beside the others (<see cref="InputRunner"/>),
/// so a slow, failing or unbuildable input never holds up another and never stops the Worker host.
/// </summary>
internal sealed class InputScheduler(IServiceScopeFactory scopes, TimeProvider time, ILogger<InputScheduler> logger) : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly InputRunner _runner = new(scopes, time, logger);
    private List<ScheduledInput> _inputs = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await DiscoverAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Without inputs the Worker still processes documents; an exception here would stop its host.
            logger.SchedulerFailed(exception.GetType().Name);
            return;
        }

        logger.SchedulerStarted(_inputs.Count, (int)CheckInterval.TotalSeconds);
        if (_inputs.Count == 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(CheckInterval, time);
        try
        {
            do
            {
                StartDue(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping.
        }
        finally
        {
            // Runs end on the stopping token and never throw; wait for them, so no scope outlives the host.
            await Task.WhenAll(_inputs.Select(input => input.Running)).ConfigureAwait(false);
        }
    }

    /// <summary>Learns the registered inputs, each kind once; all are due at once.</summary>
    internal async Task<IReadOnlyList<ScheduledInput>> DiscoverAsync()
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var now = time.GetUtcNow();
            _inputs = [.. scope.ServiceProvider.GetServices<InputSourceRegistration>()
                .Select(registration => registration.Kind)
                .Distinct()
                .Select(kind => new ScheduledInput(kind, now))];
            return _inputs;
        }
    }

    /// <summary>Starts every due input that is not running yet.</summary>
    /// <returns>The runs started now.</returns>
    internal IReadOnlyList<Task> StartDue(CancellationToken stoppingToken)
    {
        var now = time.GetUtcNow();
        return [.. _inputs
            .Select(input => input.TryStart(now, now + InputRunner.RetryAfterFailure, () => _runner.RunAsync(input, stoppingToken)))
            .OfType<Task>()];
    }
}
