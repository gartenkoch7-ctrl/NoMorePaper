using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;
using static NoMorePaper.Worker.Tests.Editing.EditFlow;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>Edits tried again (M4b P1): a split goes on with the parts it stored, an append waits for an edit of the appended document.</summary>
[Collection(SharedPostgres.Name)]
public sealed class EditRetryTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASplitInterruptedBeforeItRecordedAPart_FindsThatPartAgain_AndGivesItTheEditors()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_retry_split");
        var id = await harness.DoneDocumentAsync();
        var editor = Guid.CreateVersion7();
        await using (var db = harness.OtherWorkerContext())
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO users (id, display_name, is_admin, email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
                VALUES ({editor}, 'Bea', false, false, false, false, true, 0)
                """,
                Ct);
            var document = await db.Documents.FindAsync([id], Ct);
            document!.SetEditors(ProcessorHarness.Owner, [editor]);
            await db.SaveChangesAsync(Ct);
        }

        var editId = await harness.PlanAsync(id, EditKind.Split, data => data with
        {
            Parts = [Steps((1, PageRotation.None, false)), Steps((2, PageRotation.None, false), (3, PageRotation.None, false))],
        });
        harness.Pdf.FailOnCall = 2;
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        // As if the Worker had stopped after the intake stored part 1, before the plan and the editors recorded it.
        var first = Assert.Single(await PartsAsync(harness, id));
        await using (var scope = harness.Scopes.CreateAsyncScope())
        {
            var plan = new EditData(editId) { Parts = [Steps((1, PageRotation.None, false)), Steps((2, PageRotation.None, false), (3, PageRotation.None, false))] };
            Assert.True(await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().UpdateDataAsync(id, EditDataJson.ToJson(plan), Ct));
            await using var db = harness.OtherWorkerContext();
            await db.Database.ExecuteSqlAsync($"DELETE FROM document_editors WHERE document_id = {first}", Ct);
        }

        harness.Time.Now += TimeSpan.FromMinutes(2);
        await harness.ProcessAllAsync();

        var parts = await PartsAsync(harness, id);
        Assert.Equal(2, parts.Count);
        Assert.Equal(first, parts[0]);
        await using var check = harness.OtherWorkerContext();
        var editors = await check.Documents.AsNoTracking().Where(document => parts.Contains(document.Id))
            .Select(document => document.Editors.Select(found => found.AccountId).ToList()).ToListAsync(Ct);
        Assert.All(editors, found => Assert.Equal([editor], found));
        Assert.NotNull((await harness.AnyDocumentAsync(id)).DeletedAt);
    }

    [Fact]
    public async Task AnAppend_WaitsWhileTheAppendedDocumentIsEdited()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_retry_append");
        var target = await harness.DoneDocumentAsync();
        var appended = await harness.DoneDocumentAsync();
        await harness.PlanAsync(target, EditKind.Append, data => data with { AppendedId = appended });

        // An edit of the appended document is stored, its files not moved yet.
        await using (var scope = harness.Scopes.CreateAsyncScope())
        {
            var plan = EditDataJson.ToJson(new EditData(Guid.CreateVersion7()) { Finish = [] });
            await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().SaveAsync(
                new DocumentEdit(appended, EditKind.Rebuild, plan, ProcessorHarness.Owner, ProcessorHarness.Start, false), Ct);
        }

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Empty(harness.Pdf.Composed);
        Assert.Equal("The appended document is being edited", (await harness.JobAsync(target))!.LastError);
        Assert.Null((await harness.AnyDocumentAsync(appended)).DeletedAt);

        await using (var scope = harness.Scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().DeleteAsync(appended, Ct);
        }

        harness.Time.Now += TimeSpan.FromMinutes(2);
        await harness.ProcessAllAsync();
        Assert.NotNull((await harness.AnyDocumentAsync(appended)).DeletedAt);
        Assert.Null(await harness.EditAsync(target));
    }

    // The parts of the split, oldest first.
    private static async Task<List<Guid>> PartsAsync(ProcessorHarness harness, Guid id)
    {
        await using var db = harness.OtherWorkerContext();
        return await db.Documents.AsNoTracking().Where(document => document.SplitFrom == id).OrderBy(document => document.Id)
            .Select(document => document.Id).ToListAsync(Ct);
    }
}
