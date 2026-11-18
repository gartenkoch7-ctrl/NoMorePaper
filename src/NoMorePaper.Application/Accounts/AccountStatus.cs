namespace NoMorePaper.Application.Accounts;

/// <summary>Outcome of an account use case; the Server maps each to one HTTP status.</summary>
public enum AccountStatus
{
    Succeeded,

    /// <summary>The input was refused, see <see cref="AccountResult.Problems"/>.</summary>
    Invalid,

    /// <summary>Unknown, used or expired link, or unknown account.</summary>
    NotFound,

    /// <summary>Only the administrator may do this.</summary>
    Forbidden,

    /// <summary>The household is set up already.</summary>
    Conflict,

    /// <summary>User name or emergency code wrong.</summary>
    Unauthorized,

    /// <summary>Too many wrong passwords in a row; see <see cref="AccountResult.LockedUntil"/>.</summary>
    LockedOut,
}
