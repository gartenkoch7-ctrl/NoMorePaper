namespace NoMorePaper.Application.Inputs;

/// <summary>Stored set-ups of the inputs (<c>input_connections</c>); those of a removed account go with it.</summary>
public interface IInputConnections
{
    /// <returns>The set-up, or <c>null</c> if there is none.</returns>
    Task<InputConnection?> GetAsync(InputKind kind, Guid? accountId, CancellationToken cancellationToken);

    /// <summary>Adds or replaces the set-up of <see cref="InputConnection.Kind"/> and <see cref="InputConnection.AccountId"/>.</summary>
    Task SaveAsync(InputConnection connection, CancellationToken cancellationToken);

    /// <returns><c>false</c> if there was none.</returns>
    Task<bool> DeleteAsync(InputKind kind, Guid? accountId, CancellationToken cancellationToken);

    /// <summary>All set-ups of one input, e.g. the iCloud of every account.</summary>
    Task<IReadOnlyList<InputConnection>> ListAsync(InputKind kind, CancellationToken cancellationToken);
}
