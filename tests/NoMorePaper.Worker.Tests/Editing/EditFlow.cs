using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>Steps of the edit tests: a processed document, a planned edit as a request leaves it, and what is stored.</summary>
internal static class EditFlow
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A document received and processed (OCR, recognition) by the harness.</summary>
    public static async Task<Guid> DoneDocumentAsync(this ProcessorHarness harness)
    {
        var id = await harness.AddQueuedDocumentAsync();
        await harness.ProcessAllAsync();
        Assert.Equal(ProcessingState.Done, (await harness.DocumentAsync(id)).State);
        return id;
    }

    public static IReadOnlyList<PageStep> Steps(params (int Page, PageRotation Rotation, bool Replaced)[] steps) =>
        [.. steps.Select(step => new PageStep(step.Page, step.Rotation, step.Replaced))];

    /// <summary>Plans the edit and queues its job, like the request does.</summary>
    /// <returns>The plan's edit id.</returns>
    public static async Task<Guid> PlanAsync(this ProcessorHarness harness, Guid id, EditKind kind, Func<EditData, EditData>? plan = null)
    {
        var data = (plan ?? (same => same))(new EditData(Guid.CreateVersion7()));
        await using var scope = harness.Scopes.CreateAsyncScope();
        var edits = scope.ServiceProvider.GetRequiredService<IDocumentEdits>();
        await edits.SaveAsync(new DocumentEdit(id, kind, EditDataJson.ToJson(data), ProcessorHarness.Owner, ProcessorHarness.Start, false), Ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueEditAsync(id, recognizeAfter: false, Ct));
        return data.EditId;
    }

    public static async Task<DocumentEdit?> EditAsync(this ProcessorHarness harness, Guid id)
    {
        await using var scope = harness.Scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().GetAsync(id, Ct);
    }

    /// <summary>Kinds of the trash entries of the document, oldest first.</summary>
    public static async Task<IReadOnlyList<string>> TrashKindsAsync(this ProcessorHarness harness, Guid id)
    {
        await using var db = harness.OtherWorkerContext();
        return await db.Database.SqlQuery<string>($"SELECT kind AS \"Value\" FROM trash_items WHERE document_id = {id} ORDER BY id").ToListAsync(Ct);
    }

    /// <summary>A document whether it lies in the trash or not.</summary>
    public static async Task<Document> AnyDocumentAsync(this ProcessorHarness harness, Guid id)
    {
        await using var db = harness.OtherWorkerContext();
        return await db.Documents.IgnoreQueryFilters().AsNoTracking().SingleAsync(document => document.Id == id, Ct);
    }

    public static string ArchiveFolder(this ProcessorHarness harness, Guid id) =>
        Directory.GetDirectories(Path.Combine(harness.DataRoot, "archive"), id.ToString(), SearchOption.AllDirectories).Single();

    public static async Task<string> ReadAsync(this ProcessorHarness harness, Guid id, string fileName) =>
        await File.ReadAllTextAsync(Path.Combine(harness.ArchiveFolder(id), fileName), Ct);

    public static bool EditFolderExists(this ProcessorHarness harness, Guid id) => Directory.Exists(Path.Combine(harness.DataRoot, "edits", id.ToString()));
}
