using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using NoMorePaper.Infrastructure.Trash;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Trash;

/// <summary>
/// Restoring and removing for good in Postgres and on disk (ADR-0025): the swap of edited versions, the jobs a
/// restore queues, the files a removal takes along, and that only the document's owner of the moment may act.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class TrashUseCaseTests(PostgresFixture postgres)
{
    private static readonly Guid s_owner = QueueDatabase.Owner;
    private static readonly Guid s_bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ADocumentTrashedBeforeItsOcr_ComesBack_WithItsOcrJob()
    {
        using var world = await TrashWorld.CreateAsync(postgres, "trash_restore_ocr");
        var id = await world.Data.AddQueuedDocumentAsync(QueueDatabase.Start);
        await using (var move = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Move(move, s_owner).MoveAsync(id, Ct));
        }

        Assert.Null(await world.Data.JobAsync(id));
        var item = Assert.Single(await new EfTrashStore(world.Data.Context()).ListAsync(s_owner, Ct)).Item;

        await using (var restore = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Restore(restore, s_owner).RestoreAsync(item.Id, Ct));
        }

        Assert.Equal(ProcessingState.Queued, (await world.Data.DocumentAsync(id)).State);
        Assert.Equal((JobStage.Ocr, 0), ((await world.Data.JobAsync(id))!.Stage, (await world.Data.JobAsync(id))!.Priority));
        Assert.Empty(await new EfTrashStore(world.Data.Context()).ListAsync(s_owner, Ct));
    }

    [Fact]
    public async Task AnEditedVersion_SwapsWithTheCurrentOne_OnDiskAndInTheDatabase_AndTheArchiveIsRebuilt()
    {
        using var world = await TrashWorld.CreateAsync(postgres, "trash_restore_swap");
        Guid id;
        TrashItem first;
        DocumentFile firstFile;
        await using (var db = world.Data.Context())
        {
            var document = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: "Brief");
            id = document.Id;
            firstFile = await world.EditAsync(db, document, "first");
            first = await world.FileToTrashAsync(db, document, DocumentFileKind.Edited);
            await world.EditAsync(db, document, "second");
        }

        var listed = Assert.Single(await new EfTrashStore(world.Data.Context()).ListAsync(s_owner, Ct));
        Assert.Equal((true, false), (listed.RestoreSwaps, listed.DocumentInTrash));

        await using (var restore = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Restore(restore, s_owner).RestoreAsync(first.Id, Ct));
        }

        Assert.Equal("first", world.ReadEdited(id));
        Assert.Equal(firstFile.Sha256, (await world.Data.DocumentAsync(id)).FindFile(DocumentFileKind.Edited)?.Sha256);
        var swapped = Assert.Single(await new EfTrashStore(world.Data.Context()).ListAsync(s_owner, Ct)).Item;
        Assert.Equal(TrashItemKind.Edited, swapped.Kind);
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(world.TrashFolder(swapped.Id), "edited.pdf"), Ct));
        Assert.False(Directory.Exists(world.TrashFolder(first.Id)));
        Assert.Equal(JobStage.Edit, (await world.Data.JobAsync(id))!.Stage);
        Assert.Equal(EditKind.Rebuild, (await new EfDocumentEdits(world.Data.Context()).GetAsync(id, Ct))!.Kind);
    }

    [Fact]
    public async Task TheOriginal_ComesBack_AndTheDocumentKeepsBothVersionsAgain()
    {
        using var world = await TrashWorld.CreateAsync(postgres, "trash_restore_original");
        TrashItem original;
        Guid id;
        await using (var db = world.Data.Context())
        {
            var document = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: "Brief");
            id = document.Id;
            await world.Storage.SaveInboxAsync(id, "scan.pdf", new MemoryStream([1, 2, 3]), Ct);
            await world.Storage.ArchiveOriginalAsync(id, Ct);
            await world.EditAsync(db, document, "edited");
            document.ChooseVersions(s_owner, VersionPolicy.EditedOnly);
            original = await world.FileToTrashAsync(db, document, DocumentFileKind.Original);
        }

        await using (var restore = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Restore(restore, s_owner).RestoreAsync(original.Id, Ct));
        }

        var restored = await world.Data.DocumentAsync(id);
        Assert.NotNull(restored.FindFile(DocumentFileKind.Original));
        Assert.Equal(VersionPolicy.KeepBoth, restored.VersionPolicy);
        await using var read = world.Storage.OpenRead(id, DocumentFileKind.Original);
        Assert.Equal(3, read.Length);
        Assert.Null(await world.Data.JobAsync(id));
    }

    [Fact]
    public async Task AFileOfADocumentInTheTrash_IsAConflict_AndIsListedSo()
    {
        using var world = await TrashWorld.CreateAsync(postgres, "trash_restore_conflict");
        TrashItem edited;
        Guid id;
        await using (var db = world.Data.Context())
        {
            var document = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: "Brief");
            id = document.Id;
            await world.EditAsync(db, document, "edited");
            edited = await world.FileToTrashAsync(db, document, DocumentFileKind.Edited);
        }

        await using (var move = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Move(move, s_owner).MoveAsync(id, Ct));
        }

        var entry = (await new EfTrashStore(world.Data.Context()).ListAsync(s_owner, Ct)).Single(found => found.Item.Id == edited.Id);
        Assert.Equal((false, true), (entry.RestoreSwaps, entry.DocumentInTrash));
        await using var restore = world.Data.Context();
        Assert.Equal(ChangeOutcome.Conflict, await world.Restore(restore, s_owner).RestoreAsync(edited.Id, Ct));
        Assert.True(Directory.Exists(world.TrashFolder(edited.Id)));
    }

    [Fact]
    public async Task RemovingADocumentForGood_DeletesItsRows_ItsFiles_AndTheFoldersOfItsEntries()
    {
        using var world = await TrashWorld.CreateAsync(postgres, "trash_purge_document");
        TrashItem edited;
        Guid id;
        await using (var db = world.Data.Context())
        {
            var document = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: "Brief");
            id = document.Id;
            await world.EditAsync(db, document, "first");
            edited = await world.FileToTrashAsync(db, document, DocumentFileKind.Edited);
            await world.EditAsync(db, document, "second");
        }

        await using (var move = world.Data.Context())
        {
            await world.Move(move, s_owner).MoveAsync(id, Ct);
        }

        var item = (await new EfTrashStore(world.Data.Context()).ListAsync(s_owner, Ct)).Single(found => found.Item.Kind == TrashItemKind.Document).Item;
        await using (var foreign = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.NotFound, await world.Purge(foreign, s_bob).PurgeAsync(item.Id, Ct));
        }

        await using (var purge = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Purge(purge, s_owner).PurgeAsync(item.Id, Ct));
        }

        await using var read = world.Data.Context();
        Assert.False(await read.Documents.IgnoreQueryFilters().AnyAsync(document => document.Id == id, Ct));
        Assert.Empty(await new EfTrashStore(read).ListAsync(s_owner, Ct));
        Assert.False(Directory.Exists(world.TrashFolder(edited.Id)));
        Assert.Throws<FileNotFoundException>(() => world.Storage.OpenRead(id, DocumentFileKind.Edited));
    }

    [Fact]
    public async Task AfterAHandOver_OnlyTheNewOwner_RestoresTheEntry()
    {
        using var world = await TrashWorld.CreateAsync(postgres, "trash_restore_handover");
        var id = await world.Data.AddRecognitionJobAsync(QueueDatabase.Start);
        await using (var move = world.Data.Context())
        {
            await world.Move(move, s_owner).MoveAsync(id, Ct);
        }

        await using (var db = world.Data.Context())
        {
            await db.Database.ExecuteSqlAsync($"UPDATE documents SET owner_id = {s_bob} WHERE id = {id}", Ct);
        }

        var item = Assert.Single(await new EfTrashStore(world.Data.Context()).ListAsync(s_bob, Ct)).Item;
        await using (var old = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.NotFound, await world.Restore(old, s_owner).RestoreAsync(item.Id, Ct));
        }

        await using (var restore = world.Data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await world.Restore(restore, s_bob).RestoreAsync(item.Id, Ct));
        }

        // Done without fields: its recognition, dropped with the move, is queued again.
        Assert.Equal(JobStage.Recognition, (await world.Data.JobAsync(id))!.Stage);
    }
}
