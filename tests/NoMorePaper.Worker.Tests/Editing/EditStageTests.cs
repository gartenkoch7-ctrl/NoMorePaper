using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;
using static NoMorePaper.Worker.Tests.Editing.EditFlow;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>The edit stage makes versions (M4b P1, ADR-0025): the pages the user saw, OCR without corrections, the version rule.</summary>
[Collection(SharedPostgres.Name)]
public sealed class EditStageTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APageEdit_MakesAnEditedVersion_FromTheArchive_AndKeepsTheOriginal()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_stage_pages");
        var id = await harness.DoneDocumentAsync();
        ArchiveOfInput(harness, "Seite zwei zuerst");

        await harness.PlanAsync(id, EditKind.Pages, data => data with
        {
            Steps = Steps((2, PageRotation.Right, false), (1, PageRotation.None, false)),
        });

        Assert.Equal(1, await harness.ProcessAllAsync());
        Assert.Equal([("source.pdf", 2, 90), ("source.pdf", 1, 0)], harness.Pdf.Composed.Single());
        Assert.Equal(("application/pdf", false), harness.Ocr.LastOptions);
        var document = await harness.DocumentAsync(id);
        Assert.Equal(("Seite zwei zuerst", ProcessorHarness.Start), (document.ContentText, document.EditedAt));
        Assert.Equal(ProcessorHarness.FileName, document.FindFile(DocumentFileKind.Original)!.FileName);
        Assert.Equal("Scan 1-edited.pdf", document.FindFile(DocumentFileKind.Edited)!.FileName);
        Assert.Equal("%PDF archive of edited.pdf", await harness.ReadAsync(id, "archive.pdf"));
        Assert.StartsWith("%PDF-1.7 source.pdf:2:90", await harness.ReadAsync(id, "edited.pdf"), StringComparison.Ordinal);
        Assert.Null(await harness.JobAsync(id));
        Assert.Null(await harness.EditAsync(id));
        Assert.Empty(await harness.TrashKindsAsync(id));
        Assert.False(harness.EditFolderExists(id));
    }

    [Fact]
    public async Task ASecondEdit_MovesThePreviousEditedVersionToTheTrash()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_stage_second");
        var id = await harness.DoneDocumentAsync();
        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((1, PageRotation.Half, false)) });
        await harness.ProcessAllAsync();
        var first = await harness.ReadAsync(id, "edited.pdf");

        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((1, PageRotation.Left, false)) });
        await harness.ProcessAllAsync();

        Assert.Equal(["Edited"], await harness.TrashKindsAsync(id));
        var trashed = Directory.GetFiles(Path.Combine(harness.DataRoot, "trash"), "edited.pdf", SearchOption.AllDirectories).Single();
        Assert.Equal(first, await File.ReadAllTextAsync(trashed, Ct));
        Assert.StartsWith("%PDF-1.7 source.pdf:1:270", await harness.ReadAsync(id, "edited.pdf"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyEdited_MovesTheOriginalToTheTrash()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_stage_edited_only");
        var id = await harness.DoneDocumentAsync();
        await ChooseAsync(harness, id, VersionPolicy.EditedOnly);

        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((3, PageRotation.None, false)) });
        await harness.ProcessAllAsync();

        var document = await harness.DocumentAsync(id);
        Assert.Null(document.FindFile(DocumentFileKind.Original));
        Assert.NotNull(document.FindFile(DocumentFileKind.Edited));
        Assert.Equal(["Original"], await harness.TrashKindsAsync(id));
        Assert.Empty(Directory.GetFiles(harness.ArchiveFolder(id), "original.*"));
    }

    [Fact]
    public async Task OnlyOriginal_PutsTheNewVersionInTheTrash_AndMakesTheArchiveFromTheOriginal()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_stage_original_only");
        var id = await harness.DoneDocumentAsync();
        await ChooseAsync(harness, id, VersionPolicy.OriginalOnly);
        ArchiveOfInput(harness, "Original");

        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((1, PageRotation.Right, false)) });
        await harness.ProcessAllAsync();

        var document = await harness.DocumentAsync(id);
        Assert.Null(document.FindFile(DocumentFileKind.Edited));
        Assert.Equal(["Edited"], await harness.TrashKindsAsync(id));
        Assert.Equal(("application/pdf", true), harness.Ocr.LastOptions);
        Assert.Equal("%PDF archive of source-file", await harness.ReadAsync(id, "archive.pdf"));
        Assert.False(File.Exists(Path.Combine(harness.ArchiveFolder(id), "edited.pdf")));
    }

    [Fact]
    public async Task AReplacedPage_IsTheBrowsersImage_AsAOnePagePdf()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_stage_replaced");
        var id = await harness.DoneDocumentAsync();
        var editId = await harness.PlanAsync(id, EditKind.Pages, data => data with
        {
            Steps = Steps((1, PageRotation.None, false), (2, PageRotation.Right, true)),
        });
        await using (var scope = harness.Scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEditPageStorage>().SaveAsync(id, editId, 2, new MemoryStream(TestJpeg.Bytes()), Ct);
        }

        await harness.ProcessAllAsync();

        Assert.Equal([("source.pdf", 1, 0), ("replaced-2.pdf", 1, 90)], harness.Pdf.Composed.Single());
        Assert.NotNull((await harness.DocumentAsync(id)).FindFile(DocumentFileKind.Edited));
        Assert.False(harness.EditFolderExists(id));
    }

    [Fact]
    public async Task APlanThatDoesNotFit_IsGivenUpAtOnce_AndTheDocumentStaysAsItWas()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_stage_bad_plan");
        var id = await harness.DoneDocumentAsync();
        var before = await harness.DocumentAsync(id);
        harness.Pdf.PageCount = 2;

        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((3, PageRotation.None, false)) });
        await harness.ProcessAllAsync();

        var after = await harness.DocumentAsync(id);
        Assert.Equal(before.FindFile(DocumentFileKind.Archive)!.Sha256, after.FindFile(DocumentFileKind.Archive)!.Sha256);
        Assert.Null(after.FindFile(DocumentFileKind.Edited));
        Assert.Null(await harness.JobAsync(id));
        Assert.Null(await harness.EditAsync(id));
        Assert.Empty(harness.Pdf.Composed);
    }

    /// <summary>OCR writes an archive that names its input and recognizes <paramref name="text"/>.</summary>
    internal static void ArchiveOfInput(ProcessorHarness harness, string text) =>
        harness.Ocr.Behavior = async (input, output, cancellationToken) =>
        {
            await File.WriteAllTextAsync(output, "%PDF archive of " + Path.GetFileName(input), cancellationToken);
            return new OcrResult(text, 1);
        };

    private static async Task ChooseAsync(ProcessorHarness harness, Guid id, VersionPolicy policy)
    {
        await using var db = harness.OtherWorkerContext();
        var document = await db.Documents.FindAsync([id], Ct);
        document!.ChooseVersions(ProcessorHarness.Owner, policy);
        await db.SaveChangesAsync(Ct);
    }
}
