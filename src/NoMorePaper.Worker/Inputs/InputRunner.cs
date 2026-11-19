using System.Diagnostics;
using System.Diagnostics.Metrics;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Inputs;

/// <summary>
/// One run of one input in a scope of its own: builds only that input's source (keyed by its kind), runs it
/// and records the outcome, for the whole input and for every account part. Never throws but on the stop
/// of the Worker; whatever fails ends up as <see cref="InputProblemKind.Failed"/> and in the log.
/// </summary>
internal sealed class InputRunner(IServiceScopeFactory scopes, TimeProvider time, ILogger logger)
{
    /// <summary>Next try of an input whose source could not be built or run, as its interval is unknown then.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    private const string SourceTag = "nomorepaper.inputs.source";

    private static readonly Counter<long> s_received = WorkerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.inputs.received", description: "Documents the background inputs took in, by source");

    private static readonly Counter<long> s_failed = WorkerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.inputs.failed", description: "Input runs that ended at a problem, by source and problem");

    public async Task RunAsync(ScheduledInput input, CancellationToken stoppingToken)
    {
        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var result = await RunSourceAsync(scope.ServiceProvider, input, stoppingToken).ConfigureAwait(false);
                if (result is not null)
                {
                    Count(input.Kind, result);
                    await RecordAsync(scope.ServiceProvider, input.Kind, result, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // E.g. disposing the scope failed; the run is over either way.
            logger.InputFailed(input.Kind, exception.GetType().Name);
        }
        catch (OperationCanceledException)
        {
            // The Worker stops; the next start runs the input again.
        }
    }

    /// <returns>The outcome, or <c>null</c> when the Worker stopped during the run.</returns>
    private async Task<InputRunResult?> RunSourceAsync(IServiceProvider services, ScheduledInput input, CancellationToken stoppingToken)
    {
        using var activity = WorkerTelemetry.Source.StartActivity("nomorepaper.inputs.run");
        activity?.SetTag("nomorepaper.inputs.kind", input.Kind.ToString());
        var startedAt = time.GetUtcNow();
        var started = time.GetTimestamp();
        try
        {
            // Only this input's source is built: a source that cannot be built fails its own runs only.
            var source = services.GetRequiredKeyedService<IInputSource>(input.Kind);
            input.Reschedule(startedAt + source.Interval);
            var result = await source.RunAsync(stoppingToken).ConfigureAwait(false);
            var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
            logger.InputRan(input.Kind, result.Received, result.Skipped, elapsedMs);
            return result;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.InputFailed(input.Kind, exception.GetType().Name);
            return InputRunResult.Failed(new InputProblem(InputProblemKind.Failed));
        }
    }

    private void Count(InputKind kind, InputRunResult result)
    {
        var source = new KeyValuePair<string, object?>(SourceTag, kind.ToString());
        if (result.Received > 0)
        {
            s_received.Add(result.Received, source);
        }

        foreach (var problem in result.Accounts.Select(account => account.Problem).Prepend(result.Problem).OfType<InputProblem>())
        {
            logger.InputProblem(kind, problem.Kind, problem.NeedsAttention);
            s_failed.Add(1, source, new KeyValuePair<string, object?>("nomorepaper.inputs.problem", problem.Kind.ToString()));
        }
    }

    // The whole input's row, then each account's own row: a success of an account clears its problem.
    private async Task RecordAsync(IServiceProvider services, InputKind kind, InputRunResult result, CancellationToken stoppingToken)
    {
        var status = services.GetRequiredService<IInputStatusStore>();
        var at = time.GetUtcNow();
        await RecordOneAsync(status, kind, null, result.Received, result.Problem, at, stoppingToken).ConfigureAwait(false);
        foreach (var account in result.Accounts)
        {
            await RecordOneAsync(status, kind, account.AccountId, account.Received, account.Problem, at, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RecordOneAsync(
        IInputStatusStore status, InputKind kind, Guid? accountId, int received, InputProblem? problem, DateTimeOffset at, CancellationToken stoppingToken)
    {
        try
        {
            await status.RecordAsync(kind, accountId, received, problem, at, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // The database may be away for a moment, or the account removed meanwhile; also while stopping,
            // a database error only costs this one row. The next run records again.
            logger.StatusNotRecorded(kind, exception.GetType().Name);
        }
    }
}
