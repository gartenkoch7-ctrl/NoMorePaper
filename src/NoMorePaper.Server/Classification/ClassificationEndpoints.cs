using NoMorePaper.Application.Classification;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Server.Accounts;

namespace NoMorePaper.Server.Classification;

/// <summary>
/// The maintained lists under <c>/api/classification/{correspondents|document-types|tags}</c>, signed in
/// only: list (visible entries), add, rename and merge. An unknown list or an entry the account may not
/// see answers 404; renaming or merging without the right to edit every affected document answers 403.
/// The routes of document fields are in <see cref="DocumentFieldsEndpoints"/>.
/// </summary>
internal static class ClassificationEndpoints
{
    public static RouteGroupBuilder MapClassificationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/classification/{kind}");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", RenameAsync);
        group.MapPost("/merge", MergeAsync);
        return api.MapDocumentFieldsEndpoints();
    }

    private static async Task<IResult> ListAsync(string kind, ListEntries entries, CancellationToken cancellationToken)
    {
        if (ClassificationMapping.KindOf(kind) is not { } list)
        {
            return ChangeResults.NotFound();
        }

        var visible = await entries.ListAsync(list, cancellationToken).ConfigureAwait(false);
        return Results.Ok(visible.Select(entry => entry.ToDto()));
    }

    private static async Task<IResult> CreateAsync(
        string kind, CreateEntryDto body, CreateEntry create, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        if (ClassificationMapping.KindOf(kind) is not { } list)
        {
            return ChangeResults.NotFound();
        }

        var created = await create.CreateAsync(list, body.Name, cancellationToken).ConfigureAwait(false);
        return created switch
        {
            { Outcome: ChangeOutcome.Changed, Entry: { } entry } => Results.Ok(entry.ToDto()),
            _ => await created.Outcome.ToResultAsync(sessions, context).ConfigureAwait(false),
        };
    }

    private static async Task<IResult> RenameAsync(
        string kind, Guid id, RenameEntryDto body, RenameEntry rename, AccountSessions sessions, HttpContext context,
        CancellationToken cancellationToken)
    {
        if (ClassificationMapping.KindOf(kind) is not { } list)
        {
            return ChangeResults.NotFound();
        }

        var outcome = await rename.RenameAsync(list, id, body.Name, cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }

    private static async Task<IResult> MergeAsync(
        string kind, MergeEntriesDto body, MergeEntries merge, AccountSessions sessions, HttpContext context,
        CancellationToken cancellationToken)
    {
        if (ClassificationMapping.KindOf(kind) is not { } list)
        {
            return ChangeResults.NotFound();
        }

        var outcome = await merge.MergeAsync(list, body.TargetId, body.SourceIds?.ToList(), cancellationToken).ConfigureAwait(false);
        return await outcome.ToResultAsync(sessions, context).ConfigureAwait(false);
    }
}
