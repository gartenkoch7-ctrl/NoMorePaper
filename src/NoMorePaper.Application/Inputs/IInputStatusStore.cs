namespace NoMorePaper.Application.Inputs;

/// <summary>The state of every input in the database (<c>input_status</c>); rows of a removed account go with it.</summary>
public interface IInputStatusStore
{
    /// <summary>Records a run that ended at <paramref name="at"/>: its problem replaces the previous one, or clears it.</summary>
    /// <param name="accountId">The account the result is about, or <c>null</c> for the whole input.</param>
    Task RecordAsync(InputKind kind, Guid? accountId, InputRunResult result, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Every row, ordered by kind, then account (the whole input first).</summary>
    Task<IReadOnlyList<InputStatus>> ListAsync(CancellationToken cancellationToken);
}
