using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary><c>GET /api/household</c>: id and display name of every member, for every signed-in account (choosing editors).</summary>
internal static class HouseholdEndpoints
{
    public static RouteGroupBuilder MapHouseholdEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/household", ListAsync);
        return api;
    }

    private static async Task<IResult> ListAsync(ListHousehold list, CancellationToken cancellationToken)
    {
        var members = await list.ListAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(members.Select(member => new HouseholdMemberDto(member.Id, member.DisplayName)));
    }
}
