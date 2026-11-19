using System.Diagnostics;
using System.Diagnostics.Metrics;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Inputs;

/// <summary>
/// Runs every registered <see cref="IInputSource"/> in its interval (ADR-0024). Checks every
/// <see cref="CheckInterval"/> which inputs are due; each run gets a scope of its own and runs beside the
/// others, so a slow or failing input never holds up another. Every run's outcome goes to
/// <see cref="IInputStatusStore"/>, an exception as <see cref="InputProblemKind.Failed"/>.
/// </summary>
internal sealed class InputScheduler(IServiceScopeFactory scopes, TimeProvider time, ILogger<InputScheduler> logger) : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private const string SourceTag = "nomorepaper.inputs.source";

    private static readonly Counter<long> s_received = WorkerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.inputs.received", description: "Documents the background inputs took in, by source");

    private static readonly Counter<long> s_failed = WorkerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.inputs.failed", description: "Input runs that ended at a problem, by source and problem");

    private List<ScheduledInput> _inputs = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _inputs = Discover();
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
        finally
        {
            // Runs end on the stopping token; wait for them, so no scope outlives the host.
            await Task.WhenAll(_inputs.Select(input => input.Running ?? Task.CompletedTask)).ConfigureAwait(false);
        }
    }

    /// <summary>Reads kind and interval of every registered source; all are due at once.</summary>
    internal List<ScheduledInput> Discover()
    {
        using var scope = scopes.CreateScope();
        var now = time.GetUtcNow();
        _inputs = [.. scope.ServiceProvider.GetServices<IInputSource>()
            .Select(source => new ScheduledInput(source.Kind, source.Interval) { DueAt = now })];
        return _inputs;
    }

    /// <summary>Starts every due input that is not running yet.</summary>
    /// <returns>The runs started now.</returns>
    internal IReadOnlyList<Task> StartDue(CancellationToken stoppingToken)
    {
        var now = time.GetUtcNow();
        var started = new List<Task>();
        foreach (var input in _inputs.Where(input => input.DueAt <= now && input.Running is not { IsCompleted: false }))
        {
            input.DueAt = now + input.Interval;
            input.Running = Task.Run(() => RunAsync(input.Kind, stoppingToken), CancellationToken.None);
            started.Add(input.Running);
        }

        return started;
    }

    /// <summary>One run of one input with its status; ends quietly on failure, rethrows nothing but the stop.</summary>
    internal async Task RunAsync(InputKind kind, CancellationToken stoppingToken)
    {
        using var activity = WorkerTelemetry.Source.StartActivity("nomorepaper.inputs.run");
        activity?.SetTag("nomorepaper.inputs.kind", kind.ToString());
        var startedAt = time.GetTimestamp();
        InputRunResult result;
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            try
            {
                var source = scope.ServiceProvider.GetServices<IInputSource>().First(found => found.Kind == kind);
                result = await source.RunAsync(stoppingToken).ConfigureAwait(false);
                var elapsedMs = (long)time.GetElapsedTime(startedAt).TotalMilliseconds;
                logger.InputRan(kind, result.Received, result.Skipped, elapsedMs);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                logger.InputFailed(kind, exception.GetType().Name);
                result = InputRunResult.Failed(new InputProblem(InputProblemKind.Failed));
            }

            Count(kind, result);
            await RecordAsync(scope.ServiceProvider, kind, result, stoppingToken).ConfigureAwait(false);
        }
    }

    private void Count(InputKind kind, InputRunResult result)
    {
        var source = new KeyValuePair<string, object?>(SourceTag, kind.ToString());
        if (result.Received > 0)
        {
            s_received.Add(result.Received, source);
        }

        if (result.Problem is { } problem)
        {
            logger.InputProblem(kind, problem.Kind, problem.NeedsAttention);
            s_failed.Add(1, source, new KeyValuePair<string, object?>("nomorepaper.inputs.problem", problem.Kind.ToString()));
        }
    }

    private async Task RecordAsync(IServiceProvider services, InputKind kind, InputRunResult result, CancellationToken stoppingToken)
    {
        try
        {
            var status = services.GetRequiredService<IInputStatusStore>();
            await status.RecordAsync(kind, result.Problem?.AccountId, result, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // The database may be away for a moment; the next run records again.
            logger.StatusNotRecorded(kind, exception.GetType().Name);
        }
    }

    /// <summary>When an input is due next and whether it runs now.</summary>
    internal sealed class ScheduledInput(InputKind kind, TimeSpan interval)
    {
        public InputKind Kind { get; } = kind;

        public TimeSpan Interval { get; } = interval;

        public DateTimeOffset DueAt { get; set; }

        public Task? Running { get; set; }
    }
}
