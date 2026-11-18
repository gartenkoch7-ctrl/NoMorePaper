namespace NoMorePaper.Application.Accounts;

/// <summary>
/// What the signed-in account is told about resets through the administrator's links. Pure logic.
/// </summary>
/// <param name="Resets">
/// Every recorded reset, oldest first, while at least one is unacknowledged; empty otherwise. The
/// whole history, not only the new ones: the administrator can sign in with a password he set and
/// confirm the notice himself, but the next reset (the member asks for one because the password no
/// longer works) shows that earlier one again.
/// </param>
/// <param name="OldestUnacknowledged">The oldest reset nobody confirmed yet; <c>null</c> when there is nothing to tell.</param>
public sealed record AdminResetNotice(IReadOnlyList<DateTimeOffset> Resets, DateTimeOffset? OldestUnacknowledged)
{
    public static readonly AdminResetNotice None = new([], null);

    public static AdminResetNotice From(IEnumerable<AdminReset> resets)
    {
        ArgumentNullException.ThrowIfNull(resets);
        var all = resets.OrderBy(reset => reset.ResetAt).ToList();
        var oldestOpen = all.FirstOrDefault(reset => reset.AcknowledgedAt is null);
        return oldestOpen is null
            ? None
            : new AdminResetNotice([.. all.Select(reset => reset.ResetAt)], oldestOpen.ResetAt);
    }
}
