namespace NoMorePaper.Server.Accounts;

/// <summary>All account routes; they join the <c>/api</c> group, which requires sign-in unless a route says otherwise.</summary>
internal static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api) =>
        api.MapSetupEndpoints()
            .MapAuthEndpoints()
            .MapInvitationEndpoints()
            .MapMemberEndpoints();
}
