using System.Text.Json;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// The answer of <c>GET /api/auth/me</c>: the account, and every reset by the administrator's link
/// that the member has not confirmed yet, oldest first (empty when there is none).
/// </summary>
public sealed record SignedInAccount(AccountDto Account, IReadOnlyList<DateTimeOffset> AdminResets)
{
    /// <summary>
    /// Reads the answer. The list comes from <c>AdminResets</c>; if it is missing or empty, the single
    /// <c>AdminResetAt</c> still gives a notice.
    /// </summary>
    /// <returns>The account, or <c>null</c> when the answer is no readable account object.</returns>
    public static SignedInAccount? Read(JsonElement json)
    {
        AccountDto? account;
        try
        {
            account = json.ValueKind == JsonValueKind.Object
                ? json.Deserialize<AccountDto>(JsonSerializerOptions.Web)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }

        if (account is null)
        {
            return null;
        }

        var resets = new List<DateTimeOffset>(account.AdminResets ?? []);
        if (resets.Count == 0 && account.AdminResetAt is { } single)
        {
            resets.Add(single);
        }

        resets.Sort();
        return new SignedInAccount(account, resets);
    }
}
