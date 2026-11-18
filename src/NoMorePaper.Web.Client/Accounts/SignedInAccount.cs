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
    /// Reads the answer. The list comes from <c>adminResets</c>; a server that sends only the
    /// single <c>adminResetAt</c> still gets its notice, so the client works with both.
    /// </summary>
    /// <returns>The account, or <c>null</c> when the answer is no account object.</returns>
    public static SignedInAccount? Read(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || json.Deserialize<AccountDto>(JsonSerializerOptions.Web) is not { } account)
        {
            return null;
        }

        var resets = DatesOf(json, "adminResets");
        if (resets.Count == 0 && account.AdminResetAt is { } single)
        {
            resets.Add(single);
        }

        resets.Sort();
        return new SignedInAccount(account, resets);
    }

    private static List<DateTimeOffset> DatesOf(JsonElement json, string property)
    {
        var dates = new List<DateTimeOffset>();
        if (!json.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return dates;
        }

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.TryGetDateTimeOffset(out var at))
            {
                dates.Add(at);
            }
        }

        return dates;
    }
}
