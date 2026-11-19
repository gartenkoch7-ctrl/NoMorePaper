using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Worker.Inputs;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;

namespace NoMorePaper.Worker.Tests.Inputs;

/// <summary>The scheduler runs each input in its interval; one failing input never stops the others.</summary>
public sealed class InputSchedulerTests
{
    private static readonly DateTimeOffset s_start = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(s_start);
    private readonly RecordingStatus _status = new();
    private readonly FakeSource _scanner = new(InputKind.ScannerFolder, TimeSpan.FromSeconds(30));
    private readonly FakeSource _mail = new(InputKind.Mail, TimeSpan.FromMinutes(5));
    private readonly RecordingLogger<InputScheduler> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryInput_RunsAtOnce_ThenInItsInterval()
    {
        var scheduler = Scheduler();
        scheduler.Discover();

        await Task.WhenAll(scheduler.StartDue(Ct));
        Assert.Equal((1, 1), (_scanner.Runs, _mail.Runs));

        _time.Now = s_start.AddSeconds(30);
        await Task.WhenAll(scheduler.StartDue(Ct));
        Assert.Equal((2, 1), (_scanner.Runs, _mail.Runs));

        _time.Now = s_start.AddMinutes(5);
        await Task.WhenAll(scheduler.StartDue(Ct));
        Assert.Equal((3, 2), (_scanner.Runs, _mail.Runs));
        Assert.Contains(2300, _log.EventIds);
    }

    [Fact]
    public async Task AFailingInput_IsRecordedAsFailed_AndTheOthersRunOn()
    {
        _mail.Behavior = _ => throw new InvalidOperationException("server said no");
        var scheduler = Scheduler();
        scheduler.Discover();

        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Equal(1, _scanner.Runs);
        var failed = _status.Recorded.Single(entry => entry.Kind == InputKind.Mail);
        Assert.Equal(InputProblemKind.Failed, failed.Result.Problem!.Kind);
        Assert.Null(_status.Recorded.Single(entry => entry.Kind == InputKind.ScannerFolder).Result.Problem);
        Assert.Contains(2302, _log.EventIds);
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("server said no", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AProblem_IsRecordedForItsAccount_AndLogged()
    {
        var anna = Guid.CreateVersion7();
        _mail.Behavior = _ => Task.FromResult(InputRunResult.Failed(new InputProblem(InputProblemKind.CodeNeeded, anna, NeedsAttention: true)));
        var scheduler = Scheduler();
        scheduler.Discover();

        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Contains(_status.Recorded, entry => entry.Kind == InputKind.Mail && entry.AccountId == anna);
        Assert.Contains(2301, _log.EventIds);
    }

    [Fact]
    public async Task ARunningInput_IsNotStartedTwice()
    {
        var release = new TaskCompletionSource<InputRunResult>();
        _scanner.Behavior = _ => release.Task;
        var scheduler = Scheduler();
        scheduler.Discover();

        var first = scheduler.StartDue(Ct);
        _time.Now = s_start.AddMinutes(1);
        var second = scheduler.StartDue(Ct);
        release.SetResult(InputRunResult.Nothing);
        await Task.WhenAll([.. first, .. second]);

        Assert.Equal(1, _scanner.Runs);
    }

    [Fact]
    public async Task AStatusThatCannotBeRecorded_IsLogged_AndTheRunEndsQuietly()
    {
        _status.Failure = new TimeoutException();
        var scheduler = Scheduler();
        scheduler.Discover();

        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Equal(2, _log.EventIds.Count(id => id == 2303));
    }

    [Fact]
    public async Task WithoutInputs_TheSchedulerEndsAtOnce()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var scheduler = new InputScheduler(services.GetRequiredService<IServiceScopeFactory>(), _time, _log);

        await scheduler.StartAsync(Ct);
        await scheduler.ExecuteTask!;

        Assert.Contains(2304, _log.EventIds);
    }

    private InputScheduler Scheduler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInputStatusStore>(_status);
        services.AddScoped<IInputSource>(_ => _scanner);
        services.AddScoped<IInputSource>(_ => _mail);
        return new InputScheduler(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _time, _log);
    }

    private sealed class FakeSource(InputKind kind, TimeSpan interval) : IInputSource
    {
        private int _runs;

        public Func<CancellationToken, Task<InputRunResult>>? Behavior { get; set; }

        public int Runs => _runs;

        public InputKind Kind => kind;

        public TimeSpan Interval => interval;

        public Task<InputRunResult> RunAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _runs);
            return Behavior?.Invoke(cancellationToken) ?? Task.FromResult(new InputRunResult(1, 0));
        }
    }

    private sealed class RecordingStatus : IInputStatusStore
    {
        public List<(InputKind Kind, Guid? AccountId, InputRunResult Result)> Recorded { get; } = [];

        public Exception? Failure { get; set; }

        public Task RecordAsync(InputKind kind, Guid? accountId, InputRunResult result, DateTimeOffset at, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            lock (Recorded)
            {
                Recorded.Add((kind, accountId, result));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<InputStatus>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
