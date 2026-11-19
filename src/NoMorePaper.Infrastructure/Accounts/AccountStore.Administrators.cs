using Microsoft.EntityFrameworkCore;

namespace NoMorePaper.Infrastructure.Accounts;

// The administrator as the owner of documents whose owner is unknown (M4, ADR-0024).
public sealed partial class AccountStore
{
    /// <inheritdoc />
    public async Task<Guid?> FindAdministratorIdAsync(CancellationToken cancellationToken)
    {
        // Version 7 ids sort by creation time: the oldest administrator, should there ever be several.
        var ids = await _db.Users.AsNoTracking()
            .Where(user => user.IsAdmin)
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .Take(1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.Count == 0 ? null : ids[0];
    }
}
