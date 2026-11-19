using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// The one way to the local LLM (ADR-0024): recognition and the mail check share one Ollama on the CPU,
/// so only one request runs at a time. A waiting recognition goes before every waiting mail; among
/// equals, first come first served. Time spent waiting here does not count towards a request's time
/// limit, which starts once the gate is held.
/// </summary>
/// <remarks>Singleton of the Worker.</remarks>
public sealed class LlmGate(TimeProvider time, ILogger<LlmGate> logger)
{
    private readonly Lock _lock = new();
    private readonly LinkedList<Waiter> _recognition = new();
    private readonly LinkedList<Waiter> _mail = new();
    private bool _held;

    /// <summary>Waits until the LLM is free for the caller; dispose the result to free it again.</summary>
    /// <param name="maxWait">Longest wait; after it, nothing is asked.</param>
    /// <exception cref="LlmBusyException">The LLM stayed busy for <paramref name="maxWait"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IDisposable> EnterAsync(LlmPriority priority, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var queue = priority == LlmPriority.Recognition ? _recognition : _mail;
        Waiter waiter;
        lock (_lock)
        {
            if (!_held && (priority == LlmPriority.Recognition || _recognition.Count == 0))
            {
                _held = true;
                return new Lease(this);
            }

            waiter = new Waiter();
            waiter.Node = queue.AddLast(waiter);
        }

        var started = time.GetTimestamp();
        using var timeout = new CancellationTokenSource(maxWait, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using (linked.Token.Register(() => GiveUp(waiter)))
        {
            try
            {
                await waiter.Granted.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.LlmBusy(priority, (long)time.GetElapsedTime(started).TotalMilliseconds);
                throw new LlmBusyException($"The local LLM stayed busy for {maxWait.TotalSeconds} s.");
            }
        }

        return new Lease(this);
    }

    // Under the lock, so a waiter is either handed the gate or given up, never both.
    private void GiveUp(Waiter waiter)
    {
        lock (_lock)
        {
            if (waiter.Node?.List is { } list)
            {
                list.Remove(waiter.Node);
                waiter.Granted.TrySetCanceled();
            }
        }
    }

    private void Release()
    {
        lock (_lock)
        {
            var next = _recognition.First ?? _mail.First;
            if (next is null)
            {
                _held = false;
                return;
            }

            next.List!.Remove(next);
            next.Value.Granted.TrySetResult();
        }
    }

    private sealed class Waiter
    {
        public TaskCompletionSource Granted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LinkedListNode<Waiter>? Node { get; set; }
    }

    private sealed class Lease(LlmGate gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
