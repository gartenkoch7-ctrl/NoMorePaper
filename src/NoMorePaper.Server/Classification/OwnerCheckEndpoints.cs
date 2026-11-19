using NoMorePaper.Application.Classification;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Inputs;

namespace NoMorePaper.Server.Classification;

/// <summary>
/// The owner check of documents whose owner was unknown on arrival (ADR-0024):
/// <c>POST /api/documents/{id}/owner-check</c> keeps it ("fits"), <c>POST /api/documents/{id}/hand-over</c>
/// with <c>{ accountId }</c> gives it to another member. 404 for a document the account may not see, 403
/// for one it does not own, 400 for an unknown or own account, 409 when nothing waits for a check.
/// </summary>
internal static class OwnerCheckEndpoints
{
    public static RouteGroupBuilder MapOwnerCheckEndpoints(this RouteGroupBuilder api)
    {
        var document = api.MapGroup("/documents/{id:guid}");
        document.MapPost("/owner-check", ConfirmAsync);
        document.MapPost("/hand-over", HandOverAsync);
        return api;
    }

    private static async Task<IResult> ConfirmAsync(
        Guid id, ConfirmOwnerCheck confirm, AccountSessions sessions, HttpContext context, ILogger<ConfirmOwnerCheck> logger,
        CancellationToken cancellationToken)
    {
        var outcome = await confirm.ConfirmAsync(id, cancellationToken).ConfigureAwait(false);
        logger.OwnerCheckAnswered("confirm", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> HandOverAsync(
        Guid id, HandOverRequestDto? body, HandOverDocument handOver, AccountSessions sessions, HttpContext context,
        ILogger<HandOverDocument> logger, CancellationToken cancellationToken)
    {
        if (body is not { AccountId: var target } || target == Guid.Empty)
        {
            logger.HandOverWithoutAccount(id);
            return ChangeResults.Failure(ChangeOutcome.Invalid);
        }

        var outcome = await handOver.HandOverAsync(id, target, cancellationToken).ConfigureAwait(false);
        logger.OwnerCheckAnswered("hand-over", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }
}
