using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Separation;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Classification;

namespace NoMorePaper.Server.Editing;

/// <summary>
/// The answer to a split suggestion in "to review" (M4b, stack separation):
/// <c>POST /api/documents/{id}/split-suggestion/accept</c> queues the split (202),
/// <c>POST /api/documents/{id}/split-suggestion/dismiss</c> keeps the stack as one document (204). 404 for a
/// document the account may not see, 403 for one it may not edit, 409 without a suggestion (already
/// answered) and, for accepting, while an edit runs or when the pages changed since.
/// </summary>
internal static class SplitSuggestionEndpoints
{
    public static RouteGroupBuilder MapSplitSuggestionEndpoints(this RouteGroupBuilder api)
    {
        var suggestion = api.MapGroup("/documents/{id:guid}/split-suggestion");
        suggestion.MapPost("/accept", AcceptAsync);
        suggestion.MapPost("/dismiss", DismissAsync);
        return api;
    }

    private static async Task<IResult> AcceptAsync(
        Guid id, AnswerSplitSuggestion answer, AccountSessions sessions, HttpContext context, ILogger<AnswerSplitSuggestion> logger,
        CancellationToken cancellationToken)
    {
        var outcome = await answer.AcceptAsync(id, cancellationToken).ConfigureAwait(false);
        logger.SplitSuggestionAnswered("accept", id, outcome);

        // The split runs in the Worker; the parts appear in "to review" once they are recognized.
        return outcome == ChangeOutcome.Changed ? Results.Accepted() : await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> DismissAsync(
        Guid id, AnswerSplitSuggestion answer, AccountSessions sessions, HttpContext context, ILogger<AnswerSplitSuggestion> logger,
        CancellationToken cancellationToken)
    {
        var outcome = await answer.DismissAsync(id, cancellationToken).ConfigureAwait(false);
        logger.SplitSuggestionAnswered("dismiss", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }
}
