using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

/// <summary>One request to the LLM at a time; recognition before mail; giving up after the longest wait.</summary>
public sealed class LlmGateTests
{
    private static readonly TimeSpan s_long = TimeSpan.FromMinutes(5);

    private readonly LlmGate _gate = new(TimeProvider.System, NullLogger<LlmGate>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OnlyOneHoldsTheGate_TheNextGetsItOnRelease()
    {
        var first = await _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);
        var second = _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);

        await Task.Delay(50, Ct);
        Assert.False(second.IsCompleted);

        first.Dispose();
        first.Dispose();
        (await second).Dispose();
        (await _gate.EnterAsync(LlmPriority.Mail, s_long, Ct)).Dispose();
    }

    [Fact]
    public async Task AWaitingRecognition_GoesBeforeEveryWaitingMail()
    {
        var order = new List<string>();
        var held = await _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);
        var mail = Take(LlmPriority.Mail, "mail");
        await Task.Delay(20, Ct);
        var recognition = Take(LlmPriority.Recognition, "recognition");
        await Task.Delay(20, Ct);

        held.Dispose();
        await Task.WhenAll(mail, recognition);

        Assert.Equal(["recognition", "mail"], order);

        async Task Take(LlmPriority priority, string name)
        {
            using var lease = await _gate.EnterAsync(priority, s_long, Ct);
            lock (order)
            {
                order.Add(name);
            }

            await Task.Delay(10, Ct);
        }
    }

    [Fact]
    public async Task AMail_DoesNotSlipPastAWaitingRecognition_WhenTheGateIsFreed()
    {
        var held = await _gate.EnterAsync(LlmPriority.Recognition, s_long, Ct);
        var recognition = _gate.EnterAsync(LlmPriority.Recognition, s_long, Ct);
        await Task.Delay(20, Ct);

        held.Dispose();
        var mail = _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);
        var lease = await recognition;
        await Task.Delay(20, Ct);
        Assert.False(mail.IsCompleted);

        lease.Dispose();
        (await mail).Dispose();
    }

    [Fact]
    public async Task WaitingLongerThanAllowed_ThrowsBusy_AndLeavesTheQueue()
    {
        var held = await _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);

        await Assert.ThrowsAsync<LlmBusyException>(() => _gate.EnterAsync(LlmPriority.Recognition, TimeSpan.FromMilliseconds(50), Ct));

        held.Dispose();
        (await _gate.EnterAsync(LlmPriority.Mail, TimeSpan.FromMilliseconds(500), Ct)).Dispose();
    }

    [Fact]
    public async Task ACancelledWait_IsNoBusyGate()
    {
        var held = await _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);
        using var stop = new CancellationTokenSource();
        var waiting = _gate.EnterAsync(LlmPriority.Recognition, s_long, stop.Token);

        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        held.Dispose();
    }

    [Fact]
    public async Task ARecognition_KeepsItsTurn_While20SlowMailsQueue()
    {
        var mails = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var lease = await _gate.EnterAsync(LlmPriority.Mail, s_long, Ct);
            await Task.Delay(30, Ct);
        }).ToList();
        await Task.Delay(40, Ct);

        // At most the running mail is ahead of it: well within a wait of half a second.
        using (await _gate.EnterAsync(LlmPriority.Recognition, TimeSpan.FromMilliseconds(500), Ct))
        {
            Assert.Contains(mails, mail => !mail.IsCompleted);
        }

        await Task.WhenAll(mails);
    }
}
