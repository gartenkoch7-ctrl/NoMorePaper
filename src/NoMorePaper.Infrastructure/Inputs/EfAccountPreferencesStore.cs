using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Persistence;
using Npgsql;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>
/// Preferences and mail senders in Postgres. The key of <c>account_mail_senders</c> is the address, so
/// two accounts saving the same address at once cannot both succeed.
/// </summary>
public sealed class EfAccountPreferencesStore(AppDbContext db) : IAccountPreferencesStore
{
    public async Task<AccountPreferences> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var isPrivate = await db.Set<AccountPreferencesRow>().AsNoTracking()
            .Where(row => row.AccountId == accountId)
            .Select(row => (bool?)row.NewDocumentsPrivate)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var senders = await SendersOf(accountId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return isPrivate is null && senders.Count == 0
            ? AccountPreferences.Default
            : new AccountPreferences(isPrivate ?? false, senders.AsReadOnly());
    }

    public async Task<PreferencesResult> SaveAsync(Guid accountId, AccountPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var addresses = preferences.MailSenders.ToList();
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            if (await TakenAsync(accountId, addresses, cancellationToken).ConfigureAwait(false) is { } taken)
            {
                return new PreferencesResult(PreferencesOutcome.SenderTaken, taken);
            }

            try
            {
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO account_preferences (account_id, new_documents_private) VALUES ({accountId}, {preferences.NewDocumentsPrivate})
                    ON CONFLICT (account_id) DO UPDATE SET new_documents_private = EXCLUDED.new_documents_private
                    """,
                    cancellationToken).ConfigureAwait(false);
                await db.Set<MailSenderRow>().Where(row => row.AccountId == accountId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                db.Set<MailSenderRow>().AddRange(addresses.Select(address => new MailSenderRow { Address = address, AccountId = accountId }));
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (Refusal(error) is { } state)
            {
                db.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

                // Unique: another account saved one of the addresses after the check above. Foreign key:
                // the account was removed meanwhile, like any other account path answers.
                return state == PostgresErrorCodes.UniqueViolation
                    ? new PreferencesResult(PreferencesOutcome.SenderTaken, await TakenAsync(accountId, addresses, cancellationToken).ConfigureAwait(false) ?? addresses[0])
                    : new PreferencesResult(PreferencesOutcome.AccountGone);
            }

            db.ChangeTracker.Clear();
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PreferencesResult(PreferencesOutcome.Saved);
        }
    }

    public async Task<Guid?> FindOwnerOfSenderAsync(string address, CancellationToken cancellationToken)
    {
        if (AccountPreferences.NormalizeAddress(address) is not { } normalized)
        {
            return null;
        }

        return await db.Set<MailSenderRow>().AsNoTracking()
            .Where(row => row.Address == normalized)
            .Select(row => (Guid?)row.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private IQueryable<string> SendersOf(Guid accountId) =>
        db.Set<MailSenderRow>().AsNoTracking().Where(row => row.AccountId == accountId).OrderBy(row => row.Address).Select(row => row.Address);

    private Task<string?> TakenAsync(Guid accountId, List<string> addresses, CancellationToken cancellationToken) =>
        addresses.Count == 0
            ? Task.FromResult<string?>(null)
            : db.Set<MailSenderRow>().AsNoTracking()
                .Where(row => addresses.Contains(row.Address) && row.AccountId != accountId)
                .OrderBy(row => row.Address)
                .Select(row => row.Address)
                .FirstOrDefaultAsync(cancellationToken);

    // The SQL state of a refusal the save answers instead of failing: a taken address or a removed account.
    // The raw insert reports it directly, SaveChanges wrapped in a DbUpdateException.
    private static string? Refusal(Exception error)
    {
        var state = (error as PostgresException ?? error.InnerException as PostgresException)?.SqlState;
        return state is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation ? state : null;
    }
}
