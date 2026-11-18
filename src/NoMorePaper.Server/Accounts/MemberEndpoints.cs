using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>Members of the household, administrator only (others get 403 from the use cases).</summary>
internal static class MemberEndpoints
{
    public static RouteGroupBuilder MapMemberEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/members", ListAsync);
        api.MapDelete("/members/{id:guid}", RemoveAsync);
        api.MapPost("/members/{id:guid}/reset-link", CreateResetLinkAsync);
        return api;
    }

    private static async Task<IResult> ListAsync(ListMembers list, CancellationToken cancellationToken)
    {
        var result = await list.ListAsync(cancellationToken).ConfigureAwait(false);
        return result.Value is { } members
            ? Results.Ok(members.Select(member => member.ToDto()))
            : AccountResponses.Failure(result, AccountResponses.PasswordField);
    }

    private static async Task<IResult> RemoveAsync(Guid id, RemoveMember remove, CancellationToken cancellationToken)
    {
        var result = await remove.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Results.NoContent() : AccountResponses.Failure(result, AccountResponses.PasswordField);
    }

    private static async Task<IResult> CreateResetLinkAsync(
        Guid id, CreateResetLink create, HttpRequest request, CancellationToken cancellationToken)
    {
        var result = await create.CreateAsync(id, cancellationToken).ConfigureAwait(false);
        return result.Value is { } link
            ? Results.Ok(new LinkDto(AccountLinks.Build(request, AccountLinks.ResetPage, link.Token), link.ExpiresAt))
            : AccountResponses.Failure(result, AccountResponses.PasswordField);
    }
}
