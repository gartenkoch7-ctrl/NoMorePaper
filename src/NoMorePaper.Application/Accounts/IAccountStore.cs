namespace NoMorePaper.Application.Accounts;

/// <summary>
/// Accounts, invitations and reset links in the database. Each method is atomic: a link is used at
/// most once even when two requests race, and a refused input changes nothing. Tokens and codes
/// arrive here only as <see cref="SecretHash"/> values. Passwords are checked against the rules in
/// <see cref="AccountRules"/> (<see cref="AccountProblem.PasswordTooShort"/>).
/// </summary>
public interface IAccountStore
{
    Task<bool> AnyAccountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the administrator, but only while no account exists (otherwise
    /// <see cref="AccountStatus.Conflict"/>), and hands every document without owner to it.
    /// </summary>
    Task<AccountResult<HouseholdSetUp>> SetUpAsync(NewAccount administrator, string emergencyCodeHash, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the password and counts failures: after <see cref="AccountRules.MaxFailedSignIns"/> in a
    /// row the account is locked for <see cref="AccountRules.LockoutDuration"/>. Attempts on one
    /// account are serialized, so parallel guesses cannot slip past the lockout.
    /// </summary>
    Task<PasswordCheck> CheckPasswordAsync(string userName, string password, CancellationToken cancellationToken);

    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether the account exists (not removed); cheaper than <see cref="FindAsync"/>.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>All accounts, ordered by display name.</summary>
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken);

    /// <returns><c>false</c> if no such account exists.</returns>
    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ends every session of the account at its next check.</summary>
    /// <returns><c>false</c> if no such account exists.</returns>
    Task<bool> RenewSecurityStampAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Like a sign-in, the current password is refused while the account is locked and a wrong one
    /// counts towards the lockout. Success ends all other sessions and voids open reset links.
    /// </summary>
    /// <returns>
    /// The account with its new security stamp; <see cref="AccountStatus.LockedOut"/> while locked (also
    /// for the guess that locks it); <see cref="AccountStatus.NotFound"/> for an unknown id.
    /// </returns>
    Task<AccountResult<Account>> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task AddInvitationAsync(string tokenHash, Guid createdBy, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <returns>When the invitation expires, or <c>null</c> if it is unknown, used or expired at <paramref name="now"/>.</returns>
    Task<DateTimeOffset?> FindOpenInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Uses the invitation and creates the member; if the member is refused, the invitation stays open.</summary>
    /// <returns>The new member; <see cref="AccountStatus.NotFound"/> if the invitation is not open at <paramref name="now"/>.</returns>
    Task<AccountResult<Account>> AcceptInvitationAsync(string tokenHash, DateTimeOffset now, NewAccount member, CancellationToken cancellationToken);

    /// <returns><c>false</c> if no such account exists.</returns>
    Task<bool> AddResetTokenAsync(Guid accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Uses the reset link and sets the password; ends all sessions and any lockout and voids the
    /// account's other reset links. A refused password leaves the link usable. Appends an
    /// <see cref="AdminReset"/> at <paramref name="now"/>, so the member is told at the next sign-in.
    /// </summary>
    /// <returns>The account id; <see cref="AccountStatus.NotFound"/> if the link is not open at <paramref name="now"/>.</returns>
    Task<AccountResult<Guid>> ResetPasswordAsync(string tokenHash, DateTimeOffset now, string newPassword, CancellationToken cancellationToken);

    /// <summary>Every reset of the account through a reset link, acknowledged or not; empty for an unknown id.</summary>
    Task<IReadOnlyList<AdminReset>> ListAdminResetsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks every unacknowledged reset of the account as seen at <paramref name="now"/>; never deletes one.</summary>
    /// <returns><c>false</c> if no such account exists.</returns>
    Task<bool> AcknowledgeAdminResetsAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the emergency code of the account (constant time); if it matches, sets the password,
    /// replaces the code and ends all sessions and any lockout.
    /// </summary>
    /// <returns>The account id; <see cref="AccountStatus.Unauthorized"/> for an unknown name or wrong code.</returns>
    Task<AccountResult<Guid>> ResetWithEmergencyCodeAsync(
        string userName, string codeHash, string newPassword, string newCodeHash, CancellationToken cancellationToken);
}
