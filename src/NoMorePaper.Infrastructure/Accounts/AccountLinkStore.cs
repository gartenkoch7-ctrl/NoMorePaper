using Microsoft.EntityFrameworkCore;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// Invitations and reset links of <see cref="AccountStore"/>. Using a link is a single conditional
/// statement, so of two racing requests exactly one gets it; callers run it in their transaction.
/// </summary>
internal sealed class AccountLinkStore(AppDbContext db, TimeProvider clock)
{
    public async Task AddInvitationAsync(string tokenHash, Guid createdBy, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        db.Invitations.Add(new Invitation
        {
            Id = Guid.CreateVersion7(),
            TokenHash = tokenHash,
            CreatedBy = createdBy,
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<DateTimeOffset?> FindOpenInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Invitations.AsNoTracking()
            .Where(invitation => invitation.TokenHash == tokenHash && invitation.UsedAt == null && invitation.ExpiresAt > now)
            .Select(invitation => (DateTimeOffset?)invitation.ExpiresAt)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Marks an open invitation used; the row stays locked until the caller's transaction ends.</summary>
    /// <returns><c>false</c> if the invitation is unknown, used or expired.</returns>
    public async Task<bool> UseInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Invitations
            .Where(invitation => invitation.TokenHash == tokenHash && invitation.UsedAt == null && invitation.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invitation => invitation.UsedAt, (DateTimeOffset?)now), cancellationToken)
            .ConfigureAwait(false) == 1;

    /// <returns><c>false</c> if the account does not exist.</returns>
    public async Task<bool> AddResetTokenAsync(Guid accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(user => user.Id == accountId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        db.PasswordResetTokens.Add(new PasswordResetToken { TokenHash = tokenHash, UserId = accountId, ExpiresAt = expiresAt });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Deletes an open reset link; the caller's rollback brings it back.</summary>
    /// <returns>The account it was for, or <c>null</c> if the link is unknown or expired.</returns>
    public async Task<Guid?> UseResetTokenAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var users = await db.Database
            .SqlQuery<Guid>($"""
                DELETE FROM password_reset_tokens
                WHERE token_hash = {tokenHash} AND expires_at > {now}
                RETURNING user_id AS "Value"
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return users.Count == 0 ? null : users[0];
    }
}
