using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Worker.Inputs;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;

namespace NoMorePaper.Worker.Tests.Inputs;

/// <summary>The scheduler runs each input in its interval; one failing input never stops the others or the host.</summary>
public sealed class InputSchedulerTests
{
    private static readonly DateTimeOffset s_start = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(s_start);
    private readonly RecordingStatus _status = new();
    private readonly FakeInputSource _scanner = new(InputKind.ScannerFolder, TimeSpan.FromSeconds(30));
    private readonly FakeInputSource _mail = new(InputKind.Mail, TimeSpan.FromMinutes(5));
    private readonly RecordingLogger<InputScheduler> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryInput_RunsAtOnce_ThenInItsInterval()
    {
        var scheduler = await StartedAsync();

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
        var scheduler = await StartedAsync();

        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Equal(1, _scanner.Runs);
        Assert.Equal(InputProblemKind.Failed, _status.Single(InputKind.Mail, null).Problem!.Kind);
        Assert.Null(_status.Single(InputKind.ScannerFolder, null).Problem);
        Assert.Contains(2302, _log.EventIds);
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("server said no", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASourceThatCannotBeBuilt_FailsOnlyItsOwnRuns_AndIsTriedAgainLater()
    {
        var scheduler = await StartedAsync(brokenMail: true);

        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Equal(1, _scanner.Runs);
        Assert.Equal(InputProblemKind.Failed, _status.Single(InputKind.Mail, null).Problem!.Kind);

        _time.Now = s_start + InputRunner.RetryAfterFailure;
        Assert.Equal(2, scheduler.StartDue(Ct).Count);
    }

    [Fact]
    public async Task ASourceThatCannotBeBuilt_DoesNotStopTheHost()
    {
        using var scheduler = new InputScheduler(Services(brokenMail: true).GetRequiredService<IServiceScopeFactory>(), _time, _log);

        await scheduler.StartAsync(Ct);
        await _scanner.FirstRun.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await scheduler.StopAsync(Ct);

        Assert.False(scheduler.ExecuteTask!.IsFaulted);
        Assert.Contains(2302, _log.EventIds);
    }

    [Fact]
    public async Task AccountResults_AreRecordedUnderTheirAccount_SoASuccessClearsTheProblem()
    {
        var anna = Guid.CreateVersion7();
        var problem = new InputProblem(InputProblemKind.CodeNeeded, NeedsAttention: true);
        _mail.Behavior = _ => Task.FromResult(new InputRunResult(0, 0) { Accounts = [new InputAccountResult(anna, 0, 0, problem)] });
        var scheduler = await StartedAsync();
        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Equal(problem, _status.Single(InputKind.Mail, anna).Problem);
        Assert.Null(_status.Single(InputKind.Mail, null).Problem);
        Assert.Contains(2301, _log.EventIds);

        _mail.Behavior = _ => Task.FromResult(new InputRunResult(2, 0) { Accounts = [new InputAccountResult(anna, 2, 0)] });
        _time.Now = s_start.AddMinutes(5);
        await Task.WhenAll(scheduler.StartDue(Ct));

        Assert.Equal((2, (InputProblem?)null), _status.Last(InputKind.Mail, anna));
    }

    [Fact]
    public async Task ARunningInput_IsNotStartedTwice()
    {
        var release = new TaskCompletionSource<InputRunResult>();
        _scanner.Behavior = _ => release.Task;
        var scheduler = await StartedAsync();

        var first = scheduler.StartDue(Ct);
        _time.Now = s_start.AddMinutes(1);
        var second = scheduler.StartDue(Ct);
        release.SetResult(InputRunResult.Nothing);
        await Task.WhenAll([.. first, .. second]);

        Assert.Equal(1, _scanner.Runs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStatusThatCannotBeRecorded_IsLogged_EvenWhileStopping(bool stopping)
    {
        _status.Failure = new InvalidOperationException("database gone");
        using var stop = new CancellationTokenSource();
        if (stopping)
        {
            _scanner.Behavior = _ =>
            {
                stop.Cancel();
                return Task.FromResult(InputRunResult.Nothing);
            };
        }

        var scheduler = await StartedAsync();
        await Task.WhenAll(scheduler.StartDue(stop.Token));

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

    private async Task<InputScheduler> StartedAsync(bool brokenMail = false)
    {
        var scheduler = new InputScheduler(Services(brokenMail).GetRequiredService<IServiceScopeFactory>(), _time, _log);
        await scheduler.DiscoverAsync();
        return scheduler;
    }

    private ServiceProvider Services(bool brokenMail)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInputStatusStore>(_status);
        services.AddKeyedScoped<IInputSource>(InputKind.ScannerFolder, (_, _) => _scanner);
        services.AddKeyedScoped<IInputSource>(InputKind.Mail, (_, _) => brokenMail ? throw new InvalidOperationException("no config") : _mail);
        services.AddSingleton(new InputSourceRegistration(InputKind.ScannerFolder));
        services.AddSingleton(new InputSourceRegistration(InputKind.Mail));
        return services.BuildServiceProvider();
    }
}
