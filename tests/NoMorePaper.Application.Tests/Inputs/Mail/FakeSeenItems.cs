using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>Seen items in memory, by list and key.</summary>
internal sealed class FakeSeenItems : ISeenItems
{
    public Dictionary<(SeenList List, string Key), int> Items { get; } = [];

    /// <summary>Marking as seen fails while set (e.g. the database is gone).</summary>
    public Exception? MarkFailure { get; set; }

    public bool Has(SeenList list, string key) => Items.ContainsKey((list, key));

    public Task<bool> HasSeenAsync(InputKind kind, Guid? accountId, SeenList list, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Has(list, key));

    public Task MarkSeenAsync(InputKind kind, Guid? accountId, SeenList list, string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (MarkFailure is not null)
        {
            throw MarkFailure;
        }

        Items.TryAdd((list, key), 0);
        return Task.CompletedTask;
    }

    public Task<int> IncrementFailuresAsync(InputKind kind, Guid? accountId, string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var failures = Items.GetValueOrDefault((SeenList.Failed, key)) + 1;
        Items[(SeenList.Failed, key)] = failures;
        return Task.FromResult(failures);
    }

    public Task ForgetAsync(InputKind kind, Guid? accountId, SeenList list, string key, CancellationToken cancellationToken)
    {
        Items.Remove((list, key));
        return Task.CompletedTask;
    }
}
