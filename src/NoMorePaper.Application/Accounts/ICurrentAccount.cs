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
}
