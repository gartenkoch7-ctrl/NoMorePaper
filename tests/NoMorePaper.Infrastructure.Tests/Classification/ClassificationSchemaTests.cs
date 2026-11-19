using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Accounts;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>The fields, tags and editors of a document in the database, and what deleting their targets does.</summary>
[Collection(SharedPostgres.Name)]
public sealed class ClassificationSchemaTests(PostgresFixture postgres)
{
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly DateTimeOffset s_now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Document_RoundTripsItsFieldsTagsAndEditors()
    {
        const string database = "classification_roundtrip";
        Guid id, correspondent, type, strom, gas, editor;
        await using (var db = await MigratedContextAsync(database))
        {
            correspondent = db.Correspondents.Add(Correspondent.Create("Stadtwerke", s_owner, s_now)).Entity.Id;
            type = db.DocumentTypes.Add(DocumentType.Create("Rechnung", s_owner, s_now)).Entity.Id;
            strom = db.Tags.Add(Tag.Create("Strom", s_owner, s_now)).Entity.Id;
            gas = db.Tags.Add(Tag.Create("Gas", s_owner, s_now)).Entity.Id;
            editor = await AddUserAsync(db);
            var document = Document.Receive("scan.pdf", s_now, s_owner, isPrivate: false);
            document.Classify(new DocumentFields("Stromrechnung", correspondent, type, new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), "Abschlag für November.", [strom]));
            document.SetEditors(s_owner, [editor]);
            db.Documents.Add(document);
            await db.SaveChangesAsync(Ct);
            id = document.Id;
        }

        await using (var db = postgres.CreateContext(database))
        {
            var loaded = await db.Documents.SingleAsync(d => d.Id == id, Ct);
            Assert.Equal(("Stromrechnung", correspondent, type), (loaded.Title, loaded.CorrespondentId, loaded.DocumentTypeId));
            Assert.Equal((new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), "Abschlag für November."), (loaded.DocumentDate, loaded.Amount, loaded.Summary));
            Assert.Equal(RecognitionState.Automatic, loaded.Recognition);
            Assert.Equal([strom], loaded.Tags.Select(tag => tag.TagId));
            Assert.Equal([editor], loaded.Editors.Select(e => e.AccountId));
            Assert.True(loaded.CanEdit(editor));

            loaded.Edit(editor, new DocumentFields(null, null, null, null, null, null, [strom, gas]));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext(database))
        {
            var loaded = await db.Documents.SingleAsync(d => d.Id == id, Ct);
            Assert.Equal((null, null, (Money?)null, RecognitionState.Confirmed), (loaded.CorrespondentId, loaded.DocumentDate, loaded.Amount, loaded.Recognition));
            // Both ids come from the same instant, so their order is random: compare sorted.
            Assert.Equal(new[] { gas, strom }.Order(), loaded.Tags.Select(tag => tag.TagId).Order());
            Assert.Equal("Confirmed", await ScalarAsync<string>(db, $"SELECT recognition AS \"Value\" FROM documents"));
        }
    }

    [Fact]
    public async Task DeletingWhatAFieldPointsTo_ClearsTheField_OrRemovesTheLink()
    {
        await using var db = await MigratedContextAsync("classification_delete");
        var correspondent = db.Correspondents.Add(Correspondent.Create("Stadtwerke", s_owner, s_now)).Entity.Id;
        var tag = db.Tags.Add(Tag.Create("Strom", s_owner, s_now)).Entity.Id;
        var editor = await AddUserAsync(db);
        var original = Document.Receive("original.pdf", s_now, s_owner, isPrivate: false);
        var copy = Document.Receive("copy.pdf", s_now.AddMinutes(1), s_owner, isPrivate: false);
        copy.Classify(new DocumentFields(null, correspondent, null, null, null, null, [tag]));
        copy.SetEditors(s_owner, [editor]);
        copy.MarkPossibleDuplicate(original.Id);
        db.Documents.AddRange(original, copy);
        await db.SaveChangesAsync(Ct);

        await db.Database.ExecuteSqlAsync($"DELETE FROM correspondents", Ct);
        await db.Database.ExecuteSqlAsync($"DELETE FROM tags", Ct);
        await db.Database.ExecuteSqlAsync($"DELETE FROM users", Ct);
        await db.Database.ExecuteSqlAsync($"DELETE FROM documents WHERE id = {original.Id}", Ct);

        await using var read = postgres.CreateContext("classification_delete");
        var loaded = await read.Documents.SingleAsync(d => d.Id == copy.Id, Ct);
        Assert.Equal((null, null), (loaded.CorrespondentId, loaded.PossibleDuplicateOf));
        Assert.Empty(loaded.Tags);
        Assert.Empty(loaded.Editors);
    }

    [Fact]
    public async Task Deleting_ADocument_RemovesItsTagsAndEditors()
    {
        await using var db = await MigratedContextAsync("classification_delete_document");
        var tag = db.Tags.Add(Tag.Create("Strom", s_owner, s_now)).Entity.Id;
        var editor = await AddUserAsync(db);
        var document = Document.Receive("scan.pdf", s_now, s_owner, isPrivate: false);
        document.Classify(new DocumentFields(null, null, null, null, null, null, [tag]));
        document.SetEditors(s_owner, [editor]);
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);

        await db.Database.ExecuteSqlAsync($"DELETE FROM documents", Ct);

        Assert.Equal(0, await ScalarAsync<int>(db, $"SELECT count(*)::int AS \"Value\" FROM document_tags"));
        Assert.Equal(0, await ScalarAsync<int>(db, $"SELECT count(*)::int AS \"Value\" FROM document_editors"));
        Assert.Equal(1, await ScalarAsync<int>(db, $"SELECT count(*)::int AS \"Value\" FROM tags"));
    }

    [Fact]
    public async Task OriginalHashes_AndNormalizedNames_AreIndexed()
    {
        await using var db = await MigratedContextAsync("classification_indexes");

        var indexes = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync(Ct);

        Assert.Contains(indexes, definition =>
            definition.Contains("ON public.document_files USING btree (sha256) WHERE ((kind)::text = 'Original'::text)", StringComparison.Ordinal));
        foreach (var table in new[] { "correspondents", "document_types", "tags" })
        {
            Assert.Contains(indexes, definition => definition.Contains($"ON public.{table} USING btree (normalized_name)", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task OriginalHashes_FindTheDocument()
    {
        await using var db = await MigratedContextAsync("classification_sha256");
        var document = Document.Receive("scan.pdf", s_now, s_owner, isPrivate: false);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "scan.pdf", "application/pdf", 1, Sha256));
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);

        var found = await db.Documents
            .Where(d => d.Files.Any(file => file.Kind == DocumentFileKind.Original && file.Sha256 == Sha256))
            .Select(d => d.Id)
            .ToListAsync(Ct);

        Assert.Equal([document.Id], found);
    }

    private static async Task<Guid> AddUserAsync(AppDbContext db)
    {
        var user = new AccountUser { Id = Guid.CreateVersion7(), UserName = "editor", DisplayName = "Editor" };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    private static Task<T> ScalarAsync<T>(AppDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<T>(sql).SingleAsync(Ct);

    private async Task<AppDbContext> MigratedContextAsync(string database)
    {
        var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return db;
    }
}
