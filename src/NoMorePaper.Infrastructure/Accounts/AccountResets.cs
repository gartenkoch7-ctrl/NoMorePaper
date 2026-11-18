using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// Forgotten passwords for <see cref="AccountStore"/>: the administrator's reset link, the
/// administrator's emergency code, and the append-only record of resets through a link
/// (<see cref="AdminPasswordReset"/>) that the member is told about.
/// </summary>
internal sealed class AccountResets(AppDbContext db, AccountPasswords passwords, AccountLinkStore links)
{
    public async Task<AccountResult<Guid>> ResetPasswordAsync(
        string tokenHash, DateTimeOffset now, string newPassword, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var userId = await links.UseResetTokenAsync(tokenHash, now, cancellationToken).ConfigureAwait(false);
            var user = userId is { } found
                ? await passwords.LockByIdAsync(found, cancellationToken).ConfigureAwait(false)
                : null;
            if (user is null)
            {
                return AccountResult.Fail<Guid>(AccountStatus.NotFound);
            }

            // On refusal the transaction is rolled back, so the link stays usable for a second try.
            var problems = await passwords.ValidateAsync(user, newPassword).ConfigureAwait(false);
            if (problems.Length > 0)
            {
                return AccountResult.Invalid<Guid>(problems);
            }

            // Only this path records it: an own change or the emergency code is no news to the account.
            db.AdminPasswordResets.Add(new AdminPasswordReset { Id = Guid.CreateVersion7(), UserId = user.Id, ResetAt = now });
            await passwords.ApplyAsync(user, newPassword, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return AccountResult.Success(user.Id);
        }
    }

    public async Task<IReadOnlyList<AdminReset>> ListAdminResetsAsync(Guid id, CancellationToken cancellationToken) =>
        await db.AdminPasswordResets.AsNoTracking()
            .Where(reset => reset.UserId == id)
            .OrderBy(reset => reset.ResetAt)
            .Select(reset => new AdminReset(reset.ResetAt, reset.AcknowledgedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> AcknowledgeAdminResetsAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(user => user.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await db.AdminPasswordResets
            .Where(reset => reset.UserId == id && reset.AcknowledgedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(reset => reset.AcknowledgedAt, (DateTimeOffset?)now), cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task<AccountResult<Guid>> ResetWithEmergencyCodeAsync(
        string userName, string codeHash, string newPassword, string newCodeHash, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var user = await passwords.LockByNameAsync(userName, cancellationToken).ConfigureAwait(false);

            // Constant-time comparison; an unknown name or an account without code compares against a
            // placeholder, so every refusal takes the same path.
            var stored = user?.EmergencyCodeHash;
            var matches = SecretHash.AreEqual(stored ?? new string('0', SecretHash.Length), codeHash);
            if (user is null || stored is null || !matches)
            {
                return AccountResult.Fail<Guid>(AccountStatus.Unauthorized);
            }

            var problems = await passwords.ValidateAsync(user, newPassword).ConfigureAwait(false);
            if (problems.Length > 0)
            {
                return AccountResult.Invalid<Guid>(problems);
            }

            user.EmergencyCodeHash = newCodeHash;
            await passwords.ApplyAsync(user, newPassword, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return AccountResult.Success(user.Id);
        }
    }
}
