using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Persistence;
using Npgsql;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// Accounts in Postgres through ASP.NET Identity. Every change that checks something first runs in
/// one transaction with a lock, so concurrent requests cannot both pass the check. Invitations and
/// reset links: <see cref="AccountLinkStore"/>.
/// </summary>
public sealed class AccountStore : IAccountStore
{
    // Arbitrary constant for pg_advisory_xact_lock: concurrent setups wait for each other.
    private const long SetupLock = 0x4E4D_5053_4554_5550;

    private readonly AppDbContext _db;
    private readonly UserManager<AccountUser> _users;
    private readonly AccountPasswords _passwords;
    private readonly AccountLinkStore _links;

    public AccountStore(AppDbContext db, UserManager<AccountUser> users, TimeProvider clock)
    {
        _db = db;
        _users = users;
        _passwords = new AccountPasswords(db, users);
        _links = new AccountLinkStore(db, clock);
    }

    public Task<bool> AnyAccountAsync(CancellationToken cancellationToken) => _db.Users.AnyAsync(cancellationToken);

    public async Task<AccountResult<HouseholdSetUp>> SetUpAsync(
        NewAccount administrator, string emergencyCodeHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SetupLock})", cancellationToken).ConfigureAwait(false);
            if (await _db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                return AccountResult.Fail<HouseholdSetUp>(AccountStatus.Conflict);
            }

            var user = NewUser(administrator, isAdmin: true);
            user.EmergencyCodeHash = emergencyCodeHash;
            var created = await _users.CreateAsync(user, administrator.Password).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                return AccountResult.Invalid<HouseholdSetUp>(IdentityOutcome.Problems(created));
            }

            // Documents from before accounts existed (M1) belong to the administrator, shared.
            var adopted = await _db.Documents
                .Where(document => document.OwnerId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(document => document.OwnerId, (Guid?)user.Id), cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return AccountResult.Success(new HouseholdSetUp(IdentityOutcome.ToAccount(user), adopted));
        }
    }

    public async Task<PasswordCheck> CheckPasswordAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var user = await _passwords.LockByNameAsync(userName, cancellationToken).ConfigureAwait(false);
            var check = await _passwords.CheckAsync(user, password).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return check;
        }
    }

    public async Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(found => found.Id == id, cancellationToken).ConfigureAwait(false);
        return user is null ? null : IdentityOutcome.ToAccount(user);
    }

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.NormalizedUserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. users.Select(IdentityOutcome.ToAccount)];
    }

    // The database removes claims, invitations and reset links with the account (foreign keys).
    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Users.Where(user => user.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) > 0;

    public async Task<bool> RenewSecurityStampAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(found => found.Id == id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        IdentityOutcome.EnsureSucceeded(await _users.UpdateSecurityStampAsync(user).ConfigureAwait(false));
        return true;
    }

    public async Task<AccountResult<Account>> ChangePasswordAsync(
        Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var user = await _passwords.LockByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                return AccountResult.Fail<Account>(AccountStatus.NotFound);
            }

            var result = await _passwords.ChangeAsync(user, currentPassword, newPassword, cancellationToken).ConfigureAwait(false);

            // Committed on refusal too: a wrong current password counts towards the lockout.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
    }

    public Task AddInvitationAsync(string tokenHash, Guid createdBy, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        _links.AddInvitationAsync(tokenHash, createdBy, expiresAt, cancellationToken);

    public Task<DateTimeOffset?> FindOpenInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _links.FindOpenInvitationAsync(tokenHash, now, cancellationToken);

    public async Task<AccountResult<Account>> AcceptInvitationAsync(
        string tokenHash, DateTimeOffset now, NewAccount member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            if (!await _links.UseInvitationAsync(tokenHash, now, cancellationToken).ConfigureAwait(false))
            {
                return AccountResult.Fail<Account>(AccountStatus.NotFound);
            }

            // On refusal the transaction is rolled back, so the invitation stays open for a second try.
            var user = NewUser(member, isAdmin: false);
            IdentityResult created;
            try
            {
                created = await _users.CreateAsync(user, member.Password).ConfigureAwait(false);
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two invitations accepted at once with the same name: Identity's check saw neither.
                return AccountResult.Invalid<Account>(AccountProblem.UserNameTaken);
            }

            if (!created.Succeeded)
            {
                return AccountResult.Invalid<Account>(IdentityOutcome.Problems(created));
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return AccountResult.Success(IdentityOutcome.ToAccount(user));
        }
    }

    public Task<bool> AddResetTokenAsync(Guid accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        _links.AddResetTokenAsync(accountId, tokenHash, expiresAt, cancellationToken);

    public async Task<AccountResult<Guid>> ResetPasswordAsync(
        string tokenHash, DateTimeOffset now, string newPassword, CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var userId = await _links.UseResetTokenAsync(tokenHash, now, cancellationToken).ConfigureAwait(false);
            var user = userId is { } found
                ? await _passwords.LockByIdAsync(found, cancellationToken).ConfigureAwait(false)
                : null;
            if (user is null)
            {
                return AccountResult.Fail<Guid>(AccountStatus.NotFound);
            }

            // On refusal the transaction is rolled back, so the link stays usable for a second try.
            var problems = await _passwords.ValidateAsync(user, newPassword).ConfigureAwait(false);
            if (problems.Length > 0)
            {
                return AccountResult.Invalid<Guid>(problems);
            }

            // Only this path records it: an own change or the emergency code is no news to the account.
            user.PasswordResetByAdminAt = now;
            await _passwords.ApplyAsync(user, newPassword, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return AccountResult.Success(user.Id);
        }
    }

    public async Task<bool> ClearAdminResetAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Users
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordResetByAdminAt, (DateTimeOffset?)null), cancellationToken)
            .ConfigureAwait(false) > 0;

    public async Task<AccountResult<Guid>> ResetWithEmergencyCodeAsync(
        string userName, string codeHash, string newPassword, string newCodeHash, CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var user = await _passwords.LockByNameAsync(userName, cancellationToken).ConfigureAwait(false);

            // Constant-time comparison; an unknown name or an account without code compares against a
            // placeholder, so every refusal takes the same path.
            var stored = user?.EmergencyCodeHash;
            var matches = SecretHash.AreEqual(stored ?? new string('0', SecretHash.Length), codeHash);
            if (user is null || stored is null || !matches)
            {
                return AccountResult.Fail<Guid>(AccountStatus.Unauthorized);
            }

            var problems = await _passwords.ValidateAsync(user, newPassword).ConfigureAwait(false);
            if (problems.Length > 0)
            {
                return AccountResult.Invalid<Guid>(problems);
            }

            user.EmergencyCodeHash = newCodeHash;
            await _passwords.ApplyAsync(user, newPassword, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return AccountResult.Success(user.Id);
        }
    }

    private static AccountUser NewUser(NewAccount account, bool isAdmin) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = account.UserName,
        DisplayName = account.DisplayName,
        IsAdmin = isAdmin,
    };
}
