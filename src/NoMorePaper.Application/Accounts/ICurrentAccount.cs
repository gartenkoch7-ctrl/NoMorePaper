namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The signed-in account of the current request. The Server provides it from the sign-in cookie;
/// use cases behind sign-in rely on it instead of taking an account id from the caller.
/// </summary>
public interface ICurrentAccount
{
    /// <exception cref="InvalidOperationException">Nobody is signed in.</exception>
    Guid Id { get; }

    /// <summary>Whether the account set up the household and may manage its members.</summary>
    /// <exception cref="InvalidOperationException">Nobody is signed in.</exception>
    bool IsAdmin { get; }

    /// <summary>
    /// Asks the store whether the account still exists. The session itself is checked only every few
    /// minutes, so use cases that change something ask first: a removed member's cookie must not
    /// change anything in the meantime (<see cref="IAccountStore.ExistsAsync"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Nobody is signed in.</exception>
    Task<bool> ExistsAsync(CancellationToken cancellationToken);
}
