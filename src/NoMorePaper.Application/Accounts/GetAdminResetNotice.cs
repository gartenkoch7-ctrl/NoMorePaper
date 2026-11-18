namespace NoMorePaper.Application.Accounts;

/// <summary>The notice about resets through the administrator's links for the signed-in account (<see cref="AdminResetNotice"/>).</summary>
public sealed class GetAdminResetNotice(IAccountStore accounts, ICurrentAccount current)
{
    public async Task<AdminResetNotice> GetAsync(CancellationToken cancellationToken) =>
        AdminResetNotice.From(await accounts.ListAdminResetsAsync(current.Id, cancellationToken).ConfigureAwait(false));
}
