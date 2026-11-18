namespace NoMorePaper.Application.Accounts;

/// <summary>Result of checking a user name and password at sign-in.</summary>
/// <param name="Account">The signed-in account; set only when <see cref="Status"/> is <see cref="PasswordCheckStatus.Succeeded"/>.</param>
/// <param name="AccountId">The account the name belongs to, for logs; <c>null</c> for an unknown name.</param>
/// <param name="LockedUntil">End of the lockout; set only when <see cref="Status"/> is <see cref="PasswordCheckStatus.LockedOut"/>.</param>
public sealed record PasswordCheck(PasswordCheckStatus Status, Account? Account, Guid? AccountId, DateTimeOffset? LockedUntil)
{
    public static PasswordCheck Success(Account account) => new(PasswordCheckStatus.Succeeded, account, account.Id, null);

    public static PasswordCheck Failure(Guid? accountId) => new(PasswordCheckStatus.Failed, null, accountId, null);

    public static PasswordCheck Locked(Guid accountId, DateTimeOffset until) => new(PasswordCheckStatus.LockedOut, null, accountId, until);
}
