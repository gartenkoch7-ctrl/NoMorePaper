namespace NoMorePaper.Application.Inputs;

/// <summary>
/// Items an input has seen (<c>input_seen_items</c>), by a key of the input (Message-ID, iCloud file id).
/// The key is stored only as its SHA-256, so the table names no mail or file. Rows of a removed account go with it.
/// </summary>
public interface ISeenItems
{
    Task<bool> HasSeenAsync(InputKind kind, Guid? accountId, SeenList list, string key, CancellationToken cancellationToken);

    /// <summary>Remembers the item in the list; already there is fine.</summary>
    Task MarkSeenAsync(InputKind kind, Guid? accountId, SeenList list, string key, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Counts one more failure of the item (list <see cref="SeenList.Failed"/>).</summary>
    /// <returns>The failures so far, this one included.</returns>
    Task<int> IncrementFailuresAsync(InputKind kind, Guid? accountId, string key, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Forgets the item in the list, e.g. its failures once it was put aside.</summary>
    Task ForgetAsync(InputKind kind, Guid? accountId, SeenList list, string key, CancellationToken cancellationToken);
}
