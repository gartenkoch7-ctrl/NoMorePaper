using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;
using static NoMorePaper.Worker.Tests.Editing.EditFlow;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>Appending, splitting, a new archive, the page backlog, and edits interrupted around their commit (M4b P1).</summary>
[Collection(SharedPostgres.Name)]
public sealed class EditKindsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Appending_PutsBothDocumentsPagesInOne_AndTheAppendedOneInTheTrash()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_kinds_append");
        var target = await harness.DoneDocumentAsync();
        var appended = await harness.DoneDocumentAsync();
        harness.Pdf.PageCount = 2;

        await harness.PlanAsync(target, EditKind.Append, data => data with { AppendedId = appended });
        await harness.ProcessAllAsync();

        Assert.Equal(
            [("source.pdf", 1, 0), ("source.pdf", 2, 0), ("appended.pdf", 1, 0), ("appended.pdf", 2, 0)],
            harness.Pdf.Composed.Single());
        Assert.NotNull((await harness.DocumentAsync(target)).FindFile(DocumentFileKind.Edited));
        Assert.NotNull((await harness.AnyDocumentAsync(appended)).DeletedAt);
        Assert.Equal(["Document"], await harness.TrashKindsAsync(appended));
    }

    [Fact]
    public async Task Splitting_MakesNewDocuments_WithOwnerPrivacyAndEditors_AndTrashesTheDocument()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_kinds_split");
        var id = await harness.AddQueuedDocumentAsync(isPrivate: true);
        await harness.ProcessAllAsync();
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

        await harness.PlanAsync(id, EditKind.Split, data => data with
        {
            Parts = [Steps((1, PageRotation.None, false), (2, PageRotation.None, false)), Steps((3, PageRotation.Right, false))],
        });
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Equal(2, harness.Pdf.Composed.Count);
        Assert.NotNull((await harness.AnyDocumentAsync(id)).DeletedAt);
        Assert.Null(await harness.JobAsync(id));
        await using var check = harness.OtherWorkerContext();
        var parts = await check.Documents.AsNoTracking().Where(document => document.Id != id).OrderBy(document => document.Title).ToListAsync(Ct);
        Assert.Equal(2, parts.Count);
        Assert.All(parts, part =>
        {
            Assert.Equal((ProcessorHarness.Owner, true, ProcessingState.Queued), (part.OwnerId, part.IsPrivate, part.State));
            Assert.Equal([editor], part.Editors.Select(found => found.AccountId));
        });
        Assert.Equal(["Scan 1 – 1", "Scan 1 – 2"], parts.Select(part => part.Title));
    }

    [Fact]
    public async Task ARebuild_MakesTheArchiveFromTheOriginal_WithCorrections()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_kinds_rebuild");
        var id = await harness.DoneDocumentAsync();
        EditStageTests.ArchiveOfInput(harness, "Neu erkannt");

        await harness.PlanAsync(id, EditKind.Rebuild);
        await harness.ProcessAllAsync();

        Assert.Empty(harness.Pdf.Composed);
        Assert.Equal(("application/pdf", true), harness.Ocr.LastOptions);
        Assert.Equal("%PDF archive of source-file", await harness.ReadAsync(id, "archive.pdf"));
        Assert.Equal("Neu erkannt", (await harness.DocumentAsync(id)).ContentText);
    }

    [Fact]
    public async Task ThePageBacklog_ReadsThePagesOfOldDocuments_WithoutOcr()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_kinds_backlog");
        var id = await harness.DoneDocumentAsync();
        await using (var db = harness.OtherWorkerContext())
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM document_pages WHERE document_id = {id}", Ct);
        }

        var ocrCalls = harness.Ocr.Calls;
        await using (var scope = harness.Scopes.CreateAsyncScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<EnqueuePageBacklog>().RunAsync(Ct));
        }

        Assert.Equal(1, await harness.ProcessAllAsync());
        Assert.Equal(ocrCalls, harness.Ocr.Calls);
        await using var check = harness.Scopes.CreateAsyncScope();
        Assert.Equal(2, (await check.ServiceProvider.GetRequiredService<IDocumentPages>().ListAsync(id, Ct)).Count);
        Assert.Null(await harness.JobAsync(id));
    }

    [Fact]
    public async Task AFailedCommit_LeavesThePreviousVersion_AndTheNextAttemptStoresTheEdit()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_kinds_changed");
        var id = await harness.DoneDocumentAsync();
        var archiveBefore = await harness.ReadAsync(id, "archive.pdf");
        var shaBefore = (await harness.DocumentAsync(id)).FindFile(DocumentFileKind.Archive)!.Sha256;

        // While the edit is made, the archive changes in the database: the commit must refuse.
        harness.Ocr.Behavior = async (input, output, cancellationToken) =>
        {
            await using (var db = harness.OtherWorkerContext())
            {
                await db.Database.ExecuteSqlAsync($"UPDATE document_files SET size_bytes = 1, sha256 = {new string('f', 64)} WHERE document_id = {id} AND kind = 'Archive'", cancellationToken);
            }

            await File.WriteAllTextAsync(output, "%PDF new archive", cancellationToken);
            return new OcrResult("Neu", 1);
        };
        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((1, PageRotation.Right, false)) });

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Equal(archiveBefore, await harness.ReadAsync(id, "archive.pdf"));
        Assert.False(File.Exists(Path.Combine(harness.ArchiveFolder(id), "edited.pdf")));
        Assert.Equal("The documents changed while the edit was made", (await harness.JobAsync(id))!.LastError);
        Assert.NotEqual(shaBefore, (await harness.DocumentAsync(id)).FindFile(DocumentFileKind.Archive)!.Sha256);

        EditStageTests.ArchiveOfInput(harness, "Neu");
        harness.Time.Now += TimeSpan.FromMinutes(2);
        await harness.ProcessAllAsync();
        Assert.Equal("%PDF archive of edited.pdf", await harness.ReadAsync(id, "archive.pdf"));
    }

    [Fact]
    public async Task AnEditInterruptedAfterItsCommit_IsFinishedByTheNextClaim()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_kinds_resume");
        var id = await harness.DoneDocumentAsync();
        var editId = await harness.PlanAsync(id, EditKind.Rebuild);

        // As the commit leaves it: the archive staged and recorded, the plan marked as stored.
        await using (var scope = harness.Scopes.CreateAsyncScope())
        {
            var stored = await harness.Storage.StageAsync(id, editId, DocumentFileKind.Archive, new MemoryStream("%PDF staged"u8.ToArray()), Ct);
            var data = EditDataJson.ToJson(new EditData(editId) { Finish = [] });
            Assert.True(await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().UpdateDataAsync(id, data, Ct));
            await using var db = harness.OtherWorkerContext();
            await db.Database.ExecuteSqlAsync($"UPDATE document_files SET sha256 = {stored.Sha256} WHERE document_id = {id} AND kind = 'Archive'", Ct);
        }

        var ocrCalls = harness.Ocr.Calls;
        await harness.ProcessAllAsync();

        Assert.Equal(ocrCalls, harness.Ocr.Calls);
        Assert.Equal("%PDF staged", await harness.ReadAsync(id, "archive.pdf"));
        Assert.Null(await harness.EditAsync(id));
        Assert.Null(await harness.JobAsync(id));
        Assert.False(harness.EditFolderExists(id));
    }
}
