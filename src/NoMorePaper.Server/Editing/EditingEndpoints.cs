using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Classification;

namespace NoMorePaper.Server.Editing;

/// <summary>
/// Scan editing (M4b, ADR-0025) under <c>/api/documents/{id}</c>, signed in only:
/// <c>GET pages</c> (<see cref="DocumentPagesDto"/>), <c>POST edit</c> (multipart, <see cref="PageEditForm"/>),
/// <c>POST append</c> (<see cref="AppendRequestDto"/>), <c>POST split</c> (multipart like edit with a
/// <see cref="SplitPlanDto"/>, or that JSON alone) and
/// <c>PUT versions</c> (<see cref="VersionChoiceDto"/>). Queued edits answer 202, the choice of versions 204;
/// refusals 400, 403, 404 (also for another account's private document), 409 (not done, another job or
/// edit pending, documents changed), 413, or 401 for a removed account.
/// </summary>
internal static class EditingEndpoints
{
    public static RouteGroupBuilder MapEditingEndpoints(this RouteGroupBuilder api)
    {
        // No antiforgery token: bodies are bound by hand or as JSON, the sign-in cookie is SameSite=Strict,
        // and CrossSiteRequestGuard refuses changing requests from other sites.
        var document = api.MapGroup("/documents/{id:guid}");
        document.MapGet("/pages", GetPagesAsync);
        document.MapPost("/edit", EditAsync);
        document.MapPost("/append", AppendAsync);
        document.MapPost("/split", SplitAsync);
        document.MapPut("/versions", ChooseVersionsAsync);
        return api;
    }

    private static async Task<IResult> GetPagesAsync(Guid id, GetDocumentPages get, CancellationToken cancellationToken)
    {
        var pages = await get.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return pages is null ? ChangeResults.NotFound() : Results.Ok(pages.ToDto());
    }

    private static async Task<IResult> EditAsync(
        Guid id, HttpRequest request, RequestPageEdit edit, AccountSessions sessions, ILogger<RequestPageEdit> logger,
        CancellationToken cancellationToken)
    {
        var (plan, images, reason, status) = await PageEditForm.ReadPagesAsync(request, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            logger.EditRejected("edit", id, reason!);
            return Results.Problem(title: "The page plan could not be read.", statusCode: status);
        }

        var outcome = await edit.RequestAsync(id, plan, images, cancellationToken).ConfigureAwait(false);
        return await AnswerAsync(outcome, "edit", id, sessions, request.HttpContext, logger).ConfigureAwait(false);
    }

    private static async Task<IResult> AppendAsync(
        Guid id, AppendRequestDto? body, AppendDocument append, AccountSessions sessions, HttpContext context,
        ILogger<AppendDocument> logger, CancellationToken cancellationToken)
    {
        if (body is not { AppendedId: var appendedId } || appendedId == Guid.Empty)
        {
            logger.EditRejected("append", id, "body");
            return ChangeResults.Failure(ChangeOutcome.Invalid);
        }

        var outcome = await append.AppendAsync(id, appendedId, cancellationToken).ConfigureAwait(false);
        return await AnswerAsync(outcome, "append", id, sessions, context, logger).ConfigureAwait(false);
    }

    // Multipart like the edit (plan + cropped pages), or the plan alone as JSON.
    private static async Task<IResult> SplitAsync(
        Guid id, HttpRequest request, SplitDocument split, AccountSessions sessions, ILogger<SplitDocument> logger,
        CancellationToken cancellationToken)
    {
        var (plan, images, reason, status) = await PageEditForm.ReadSplitAsync(request, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            logger.EditRejected("split", id, reason!);
            return Results.Problem(title: "The split could not be read.", statusCode: status);
        }

        var outcome = await split.RequestAsync(id, plan, images, cancellationToken).ConfigureAwait(false);
        return await AnswerAsync(outcome, "split", id, sessions, request.HttpContext, logger).ConfigureAwait(false);
    }

    private static async Task<IResult> ChooseVersionsAsync(
        Guid id, VersionChoiceDto? body, ChooseVersions choose, AccountSessions sessions, HttpContext context,
        ILogger<ChooseVersions> logger, CancellationToken cancellationToken)
    {
        if (body is null || EditingMapping.ToDomain(body.Policy) is not { } policy)
        {
            logger.EditRejected("versions", id, "body");
            return ChangeResults.Failure(ChangeOutcome.Invalid);
        }

        var outcome = await choose.ChooseAsync(id, policy, cancellationToken).ConfigureAwait(false);
        logger.EditAnswered("versions", id, outcome);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    // A queued edit is accepted, not done: the Worker carries it out (202 without body).
    private static async Task<IResult> AnswerAsync(
        ChangeOutcome outcome, string action, Guid id, AccountSessions sessions, HttpContext context, ILogger logger)
    {
        logger.EditAnswered(action, id, outcome);
        return outcome == ChangeOutcome.Changed
            ? Results.Accepted()
            : await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }
}
