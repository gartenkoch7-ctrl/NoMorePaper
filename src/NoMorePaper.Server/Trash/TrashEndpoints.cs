using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Trash;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Classification;

namespace NoMorePaper.Server.Trash;

/// <summary>
/// The trash of the signed-in account (ADR-0025): <c>GET /api/trash</c> lists the entries of the documents it
/// owns, <c>POST /api/trash/{id}/restore</c> brings one back, <c>DELETE /api/trash/{id}</c> removes one for good,
/// <c>DELETE /api/trash</c> empties it; <c>POST /api/documents/{id}/trash</c> moves a document there (owner only).
/// An entry of another account answers 404 like one that does not exist; 409 when a file cannot come back now.
/// </summary>
internal static class TrashEndpoints
{
    public static RouteGroupBuilder MapTrashEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/trash", ListAsync);
        api.MapPost("/trash/{id:guid}/restore", RestoreAsync);
        api.MapDelete("/trash/{id:guid}", PurgeAsync);
        api.MapDelete("/trash", EmptyAsync);
        api.MapPost("/documents/{id:guid}/trash", MoveAsync);
        return api;
    }

    private static async Task<IResult> ListAsync(ListTrash list, ILogger<ListTrash> logger, CancellationToken cancellationToken)
    {
        var entries = await list.ListAsync(cancellationToken).ConfigureAwait(false);
        logger.TrashListed(entries.Count);
        return Results.Ok(entries.Select(entry => entry.ToDto()));
    }

    private static async Task<IResult> RestoreAsync(
        Guid id, RestoreTrashItem restore, AccountSessions sessions, HttpContext context, ILogger<RestoreTrashItem> logger,
        CancellationToken cancellationToken)
    {
        var outcome = await restore.RestoreAsync(id, cancellationToken).ConfigureAwait(false);
        logger.TrashAnswered("restore", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> PurgeAsync(
        Guid id, PurgeTrashItem purge, AccountSessions sessions, HttpContext context, ILogger<PurgeTrashItem> logger,
        CancellationToken cancellationToken)
    {
        var outcome = await purge.PurgeAsync(id, cancellationToken).ConfigureAwait(false);
        logger.TrashAnswered("purge", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> EmptyAsync(
        EmptyTrash empty, AccountSessions sessions, HttpContext context, ILogger<EmptyTrash> logger, CancellationToken cancellationToken)
    {
        var (outcome, count) = await empty.EmptyAsync(cancellationToken).ConfigureAwait(false);
        if (outcome == ChangeOutcome.Changed)
        {
            logger.TrashEmptied(count);
        }

        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> MoveAsync(
        Guid id, MoveDocumentToTrash move, AccountSessions sessions, HttpContext context, ILogger<MoveDocumentToTrash> logger,
        CancellationToken cancellationToken)
    {
        var outcome = await move.MoveAsync(id, cancellationToken).ConfigureAwait(false);
        logger.TrashAnswered("move", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }
}
