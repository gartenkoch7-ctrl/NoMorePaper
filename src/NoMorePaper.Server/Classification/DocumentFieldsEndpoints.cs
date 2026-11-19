using NoMorePaper.Application.Classification;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Server.Accounts;

namespace NoMorePaper.Server.Classification;

/// <summary>
/// Fields, editors and duplicates of a document under <c>/api/documents/{id}</c>, and <c>/api/review</c>,
/// signed in only. A document the account may not see answers 404 everywhere, like one that does not
/// exist; one it sees but may not change answers 403.
/// </summary>
internal static class DocumentFieldsEndpoints
{
    public static RouteGroupBuilder MapDocumentFieldsEndpoints(this RouteGroupBuilder api)
    {
        var document = api.MapGroup("/documents/{id:guid}");
        document.MapGet("/fields", GetFieldsAsync);
        document.MapPut("/fields", EditFieldsAsync);
        document.MapPost("/confirm", ConfirmAsync);
        document.MapPut("/editors", SetEditorsAsync);
        document.MapPost("/duplicate/keep", KeepDuplicateAsync);

        // Until the recycle bin (M4) only possible duplicates can be deleted, by their owner (409 otherwise).
        document.MapDelete("/", DeleteDuplicateAsync);
        api.MapGet("/review", ListReviewAsync);
        return api;
    }

    private static async Task<IResult> GetFieldsAsync(Guid id, GetDocumentFields get, CancellationToken cancellationToken)
    {
        var fields = await get.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return fields is null ? ChangeResults.NotFound() : Results.Ok(fields.ToDto());
    }

    private static async Task<IResult> EditFieldsAsync(
        Guid id, EditFieldsRequestDto body, EditDocumentFields edit, AccountSessions sessions, HttpContext context,
        CancellationToken cancellationToken)
    {
        var outcome = await edit.EditAsync(id, body.ToEdit(), cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> ConfirmAsync(
        Guid id, ConfirmDocument confirm, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var outcome = await confirm.ConfirmAsync(id, cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> SetEditorsAsync(
        Guid id, SetEditorsRequestDto body, SetDocumentEditors set, AccountSessions sessions, HttpContext context,
        CancellationToken cancellationToken)
    {
        var outcome = await set.SetAsync(id, body.EditorIds, cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> KeepDuplicateAsync(
        Guid id, KeepDuplicate keep, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var outcome = await keep.KeepAsync(id, cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> DeleteDuplicateAsync(
        Guid id, DeleteDuplicate delete, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var outcome = await delete.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> ListReviewAsync(ListReview list, CancellationToken cancellationToken)
    {
        var items = await list.ListAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(items.Select(item => item.ToDto()));
    }
}
