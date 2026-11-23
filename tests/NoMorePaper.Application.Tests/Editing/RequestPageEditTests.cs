using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Classification;
using NoMorePaper.Application.Tests.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using Xunit;
using static NoMorePaper.Application.Tests.Editing.EditSamples;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>Asking for a page edit or a split (ADR-0025): rights, state, plan, images, and refusals that leave nothing behind.</summary>
public sealed class RequestPageEditTests
{
    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeDocumentPages _pages = new();
    private readonly FakeDocumentEdits _edits = new();
    private readonly RequeueOnlyJobQueue _queue = new();
    private readonly FakeEditPageStorage _images = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<RequestPageEdit> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PagePlan Plan(params (int Page, bool Replaced)[] steps) =>
        new([.. steps.Select(step => new PageStep(step.Page, PageRotation.Right, step.Replaced))]);

    [Fact]
    public async Task AnEditor_QueuesTheEdit_AndTheImagesGoToTheEditsOwnFolder()
    {
        var owner = Guid.CreateVersion7();
        var document = _documents.Add(owner, isPrivate: false).Done();
        document.SetEditors(owner, [_current.Id]);
        _pages.Pages[document.Id] = [new PageInfo(1, "a", false, false), new PageInfo(2, "b", false, false)];

        var outcome = await Request().RequestAsync(document.Id, Plan((2, true), (1, false)), [Image(2)], Ct);

        Assert.Equal(ChangeOutcome.Changed, outcome);
        var edit = _edits.Edits[document.Id];
        var data = EditDataJson.Parse(edit.Data, EditKind.Pages)!;
        Assert.Equal((EditKind.Pages, _current.Id, false), (edit.Kind, edit.RequestedBy, edit.RecognizeAfter));
        Assert.Equal([2, 1], data.Steps!.Select(step => step.SourcePage));
        Assert.Equal(Jpeg, _images.Images[(document.Id, data.EditId, 2)]);
        Assert.Equal([document.Id], _queue.EditsQueued);
        Assert.Equal(1, _documents.Saves);
        Assert.Contains(5500, _log.EventIds);
    }

    [Fact]
    public async Task WhileAnotherJobRuns_TheRequestIsRefused_AndLeavesNothingBehind()
    {
        var document = _documents.Add(_current.Id, isPrivate: true).Done();
        _queue.EnqueueEditResult = false;

        Assert.Equal(ChangeOutcome.Conflict, await Request().RequestAsync(document.Id, Plan((1, true)), [Image(1)], Ct));

        Assert.Empty(_edits.Edits);
        Assert.Empty(_images.Images);
        Assert.Equal(0, _documents.Saves);
        Assert.Contains(5501, _log.EventIds);
    }

    [Fact]
    public async Task APendingEdit_RefusesTheSplit_WithoutTouchingItsPlan()
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done();
        var pending = new DocumentEdit(document.Id, EditKind.Pages, "{\"running\":true}", _current.Id, Now, false);
        await _edits.SaveAsync(pending, Ct);
        var split = new SplitPlan([Plan((1, false)), Plan((2, false))]);

        Assert.Equal(ChangeOutcome.Conflict, await new SplitDocument(Request()).RequestAsync(document.Id, split, Ct));

        Assert.Equal(pending, _edits.Edits[document.Id]);
        Assert.Empty(_queue.EditsQueued);
        Assert.Equal(0, _documents.Saves);
    }

    [Fact]
    public async Task ASplit_IsQueuedWithItsParts_AndReplacedPagesNeedTheirImages()
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done();
        var split = new SplitDocument(Request());
        var cropped = new SplitPlan([Plan((1, false)), Plan((3, true))]);

        Assert.Equal(ChangeOutcome.Invalid, await split.RequestAsync(document.Id, cropped, Ct));
        Assert.Equal(ChangeOutcome.Invalid, await split.RequestAsync(document.Id, new SplitPlan([Plan((1, false))]), Ct));
        Assert.Equal(ChangeOutcome.Changed, await split.RequestAsync(document.Id, cropped, [Image(3)], Ct));

        var data = EditDataJson.Parse(_edits.Edits[document.Id].Data, EditKind.Split)!;
        Assert.Equal([[1], [3]], data.Parts!.Select(part => part.Select(step => step.SourcePage).ToArray()));
        Assert.True(_images.Images.ContainsKey((document.Id, data.EditId, 3)));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("not-jpeg")]
    [InlineData("too-large")]
    public async Task APlanOrImagesThatDoNotFit_AreInvalid(string fault)
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done();
        _pages.Pages[document.Id] = [new PageInfo(1, "a", false, false), new PageInfo(2, "b", false, false)];
        var (plan, images) = fault switch
        {
            "count" => (Plan((3, false)), Array.Empty<EditPageImage>()),
            "missing" => (Plan((1, true)), []),
            "extra" => (Plan((1, false)), [Image(1)]),
            "not-jpeg" => (Plan((1, true)), [Image(1, "%PDF-1.7"u8.ToArray())]),
            _ => (Plan((1, true)), [new EditPageImage(1, EditPageImage.MaxBytes + 1, () => new MemoryStream(Jpeg))]),
        };

        Assert.Equal(ChangeOutcome.Invalid, await Request().RequestAsync(document.Id, plan, images, Ct));
        Assert.Empty(_edits.Edits);
        Assert.Contains(5502, _log.EventIds);
    }

    [Fact]
    public async Task AnUnprocessedDocument_IsAConflict_AndAnotherAccountsSharedOne_IsForbidden()
    {
        var queued = _documents.Add(_current.Id, isPrivate: false);
        var foreign = _documents.Add(Guid.CreateVersion7(), isPrivate: false).Done();

        Assert.Equal(ChangeOutcome.Conflict, await Request().RequestAsync(queued.Id, Plan((1, false)), [], Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await Request().RequestAsync(foreign.Id, Plan((1, false)), [], Ct));
        Assert.Empty(_queue.EditsQueued);
    }

    private RequestPageEdit Request() => new(_documents, _pages, _edits, _queue, _images, _current, new FixedClock(Now), _log);
}
