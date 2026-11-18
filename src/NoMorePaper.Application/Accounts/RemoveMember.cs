using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The administrator removes a member; its sessions end at their next check. Its documents stay:
/// shared ones remain visible, private ones stay hidden from everybody.
/// </summary>
public sealed class RemoveMember(IAccountStore accounts, ICurrentAccount current, ILogger<RemoveMember> logger)
{
    public async Task<AccountResult> RemoveAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            logger.AdministratorOnly(current.Id, "remove-member");
            return AccountResult.Fail(AccountStatus.Forbidden);
        }

        // The household would be left without anybody who can manage it.
        if (memberId == current.Id)
        {
            return AccountResult.Invalid(AccountProblem.CannotRemoveSelf);
        }

        if (!await accounts.RemoveAsync(memberId, cancellationToken).ConfigureAwait(false))
        {
            return AccountResult.Fail(AccountStatus.NotFound);
        }

        logger.MemberRemoved(current.Id, memberId);
        return AccountResult.Success();
    }
}
