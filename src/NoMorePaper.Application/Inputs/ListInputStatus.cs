using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Inputs;

/// <summary>
/// The state of the inputs for the page "Inputs": the administrator sees every row with the account's
/// name; a member sees the rows of the whole scanner folder and mailbox and its own rows (its iCloud),
/// never another account's.
/// </summary>
public sealed class ListInputStatus(IInputStatusStore status, IAccountStore accounts, ICurrentAccount current)
{
    public async Task<IReadOnlyList<InputStatusEntry>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await status.ListAsync(cancellationToken).ConfigureAwait(false);
        var visible = rows.Where(row => current.IsAdmin || row.AccountId == current.Id
            || (row.AccountId is null && row.Kind is InputKind.ScannerFolder or InputKind.Mail)).ToList();
        if (!visible.Exists(row => row.AccountId is not null))
        {
            return [.. visible.Select(row => new InputStatusEntry(row, null))];
        }

        var names = (await accounts.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(account => account.Id, account => account.DisplayName);
        return [.. visible.Select(row => new InputStatusEntry(
            row, row.AccountId is { } id && names.TryGetValue(id, out var name) ? name : null))];
    }
}
