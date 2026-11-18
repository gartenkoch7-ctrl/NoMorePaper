using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// Password work of the account store on top of Identity's <see cref="UserManager{TUser}"/>: the
/// sign-in check with lockout, the password change (whose current password counts like a sign-in)
/// and setting a password after a reset. Callers run it inside a transaction that holds the
/// account's row lock (<see cref="LockByNameAsync"/>, <see cref="LockByIdAsync"/>).
/// </summary>
internal sealed class AccountPasswords(AppDbContext db, UserManager<AccountUser> users)
{
    // Hashed once, so an unknown user name costs the same PBKDF2 work as a wrong password and the
    // answer time does not tell which names exist.
    private static readonly AccountUser s_nobody = new() { Id = Guid.Empty };
    private static readonly string s_nobodyHash = new PasswordHasher<AccountUser>().HashPassword(s_nobody, Guid.NewGuid().ToString());

    /// <summary>
    /// Locks the account's row until the transaction ends, so parallel requests for one account run
    /// one after another (no lockout bypass, no double use of an emergency code).
    /// </summary>
    /// <returns>The account, tracked, or <c>null</c> for an unknown name.</returns>
    public async Task<AccountUser?> LockByNameAsync(string userName, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeName(userName) ?? string.Empty;
        var ids = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM users WHERE normalized_user_name = {normalized} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.Count == 0
            ? null
            : await db.Users.SingleAsync(user => user.Id == ids[0], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Like <see cref="LockByNameAsync"/>, by id.</summary>
    public async Task<AccountUser?> LockByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ids = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM users WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.Count == 0
            ? null
            : await db.Users.SingleAsync(user => user.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Identity's sign-in rules: lockout first, then the password; failures count towards the lockout.</summary>
    public async Task<PasswordCheck> CheckAsync(AccountUser? user, string password)
    {
        if (user is null)
        {
            users.PasswordHasher.VerifyHashedPassword(s_nobody, s_nobodyHash, password);
            return PasswordCheck.Failure(null);
        }

        if (await users.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return Locked(user);
        }

        if (await users.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            IdentityOutcome.EnsureSucceeded(await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false));
            return PasswordCheck.Success(IdentityOutcome.ToAccount(user));
        }

        IdentityOutcome.EnsureSucceeded(await users.AccessFailedAsync(user).ConfigureAwait(false));
        return await users.IsLockedOutAsync(user).ConfigureAwait(false) ? Locked(user) : PasswordCheck.Failure(user.Id);
    }

    /// <summary>
    /// Changes the password of a signed-in account. The current password is checked like at sign-in:
    /// refused while the account is locked, and each wrong guess counts towards the lockout, so a
    /// stolen session cannot be used to guess it.
    /// </summary>
    public async Task<AccountResult<Account>> ChangeAsync(
        AccountUser user, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        if (await users.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return AccountResult.Invalid<Account>(AccountProblem.CurrentPasswordWrong);
        }

        if (!await users.CheckPasswordAsync(user, currentPassword).ConfigureAwait(false))
        {
            IdentityOutcome.EnsureSucceeded(await users.AccessFailedAsync(user).ConfigureAwait(false));
            return AccountResult.Invalid<Account>(AccountProblem.CurrentPasswordWrong);
        }

        var problems = await ValidateAsync(user, newPassword).ConfigureAwait(false);
        if (problems.Length > 0)
        {
            IdentityOutcome.EnsureSucceeded(await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false));
            return AccountResult.Invalid<Account>(problems);
        }

        await ApplyAsync(user, newPassword, cancellationToken).ConfigureAwait(false);
        return AccountResult.Success(IdentityOutcome.ToAccount(user));
    }

    /// <returns>The problems of the password, empty if it is acceptable.</returns>
    public async Task<AccountProblem[]> ValidateAsync(AccountUser user, string password)
    {
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return IdentityOutcome.Problems(result);
            }
        }

        return [];
    }

    /// <summary>
    /// Stores a password checked with <see cref="ValidateAsync"/>, ends all sessions (new security
    /// stamp), lifts any lockout and voids every other reset link of the account; saves the other
    /// pending changes of <paramref name="user"/> too.
    /// </summary>
    public async Task ApplyAsync(AccountUser user, string password, CancellationToken cancellationToken)
    {
        await VoidResetLinksAsync(user.Id, cancellationToken).ConfigureAwait(false);
        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        IdentityOutcome.EnsureSucceeded(await users.UpdateSecurityStampAsync(user).ConfigureAwait(false));
    }

    /// <summary>A reset link must not outlive a password the member chose afterwards.</summary>
    public Task VoidResetLinksAsync(Guid userId, CancellationToken cancellationToken) =>
        db.PasswordResetTokens.Where(token => token.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    private static PasswordCheck Locked(AccountUser user) =>
        PasswordCheck.Locked(user.Id, user.LockoutEnd ?? throw new InvalidOperationException("A locked account has a lockout end."));
}
