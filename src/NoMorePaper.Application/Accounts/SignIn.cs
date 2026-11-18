namespace NoMorePaper.Application.Accounts;

/// <summary>Checks user name and password; issuing the session is the host's part (cookie).</summary>
public sealed class SignIn(IAccountStore accounts)
{
    public Task<PasswordCheck> CheckAsync(string? userName, string? password, CancellationToken cancellationToken) =>
        accounts.CheckPasswordAsync(AccountInput.UserName(userName), password ?? string.Empty, cancellationToken);
}
