using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Server.Inputs;

/// <summary>
/// <c>GET /api/inputs</c>: the state of the background inputs for the page "Inputs". What an account sees
/// is decided by <see cref="ListInputStatus"/>: the administrator everything, a member only the shared
/// scanner folder and mailbox and its own rows.
/// </summary>
internal static class InputStatusEndpoints
{
    public static RouteGroupBuilder MapInputStatusEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/inputs", ListAsync);
        return api;
    }

    private static async Task<IResult> ListAsync(ListInputStatus list, CancellationToken cancellationToken)
    {
        var entries = await list.ListAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(entries.Select(entry => entry.ToDto()));
    }
}
