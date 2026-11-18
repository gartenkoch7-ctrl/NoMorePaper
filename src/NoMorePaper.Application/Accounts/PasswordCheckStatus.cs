namespace NoMorePaper.Application.Accounts;

public enum PasswordCheckStatus
{
    Succeeded,

    /// <summary>Unknown user name or wrong password; callers must not tell which.</summary>
    Failed,

    /// <summary>Too many failures in a row; even the right password is refused until the lockout ends.</summary>
    LockedOut,
}
