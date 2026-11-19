using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>Editing fields: who may, which entries may be attached, which values are refused.</summary>
public sealed class EditDocumentFieldsTests
{
    private const int Edited = 5250;
    private const int Refused = 5256;
    private const int AccountGone = 5113;

    private static readonly Guid s_correspondent = Guid.CreateVersion7();
    private static readonly Guid s_tag = Guid.CreateVersion7();

    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<EditDocumentFields> _log = new();

    public EditDocumentFieldsTests()
    {
        _documents.VisibleEntries.Add(s_correspondent);
        _documents.VisibleEntries.Add(s_tag);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOwner_SetsAllFields_WhichCountAsConfirmed()
    {
        var document = _documents.Add(_current.Id, isPrivate: true);
        var edit = new FieldsEdit("Stromrechnung", s_correspondent, null, new DateOnly(2026, 11, 1), 84.20m, "EUR", "Strom für Oktober.", [s_tag]);

        Assert.Equal(ChangeOutcome.Changed, await EditAsync(document.Id, edit));

        Assert.Equal(("Stromrechnung", s_correspondent, new Money(84.20m, "EUR")), (document.Title, document.CorrespondentId, document.Amount));
        Assert.Equal((RecognitionState.Confirmed, s_tag), (document.Recognition, Assert.Single(document.Tags).TagId));
        Assert.Equal(1, _documents.Saves);
        Assert.Equal([Edited], _log.EventIds);
    }

    [Fact]
    public async Task AChosenEditor_MayEditASharedDocument()
    {
        var owner = Guid.CreateVersion7();
        var document = _documents.Add(owner, isPrivate: false);
        document.SetEditors(owner, [_current.Id]);

        Assert.Equal(ChangeOutcome.Changed, await EditAsync(document.Id, Edit()));
    }

    [Fact]
    public async Task AnotherMember_IsForbidden_ToEditASharedDocument()
    {
        var document = _documents.Add(Guid.CreateVersion7(), isPrivate: false);

        Assert.Equal(ChangeOutcome.Forbidden, await EditAsync(document.Id, Edit()));

        Assert.Equal((0, RecognitionState.None), (_documents.Saves, document.Recognition));
        Assert.Equal([Refused], _log.EventIds);
    }

    [Fact]
    public async Task AnotherAccountsPrivateDocument_IsNotFound_EvenForAChosenEditor()
    {
        var owner = Guid.CreateVersion7();
        var document = _documents.Add(owner, isPrivate: true);
        document.SetEditors(owner, [_current.Id]);

        Assert.Equal(ChangeOutcome.NotFound, await EditAsync(document.Id, Edit()));
        Assert.Equal(0, _documents.Saves);
    }

    [Theory]
    [InlineData("correspondent")]
    [InlineData("type")]
    [InlineData("tag")]
    public async Task AnEntryTheEditorMayNotSee_IsInvalid(string field)
    {
        var document = _documents.Add(_current.Id, isPrivate: false);
        var hidden = Guid.CreateVersion7();
        var edit = field switch
        {
            "correspondent" => Edit() with { CorrespondentId = hidden },
            "type" => Edit() with { DocumentTypeId = hidden },
            _ => Edit() with { TagIds = [s_tag, hidden] },
        };

        Assert.Equal(ChangeOutcome.Invalid, await EditAsync(document.Id, edit));
        Assert.Equal((0, RecognitionState.None), (_documents.Saves, document.Recognition));
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("decimals")]
    [InlineData("no-currency")]
    [InlineData("tags-missing")]
    [InlineData("empty-title")]
    [InlineData("long-summary")]
    public async Task InvalidValues_AreRefused_WhetherOrNotTheDocumentExists(string problem)
    {
        var edit = problem switch
        {
            "currency" => Edit() with { Amount = 1m, Currency = "euro" },
            "decimals" => Edit() with { Amount = 1.23456m, Currency = "EUR" },
            "no-currency" => Edit() with { Amount = 1m },
            "tags-missing" => Edit() with { TagIds = null },
            "empty-title" => Edit() with { Title = " " },
            _ => Edit() with { Summary = new string('x', DocumentFields.MaxSummaryLength + 1) },
        };
        var document = _documents.Add(_current.Id, isPrivate: false);

        Assert.Equal(ChangeOutcome.Invalid, await EditAsync(document.Id, edit));
        Assert.Equal(ChangeOutcome.Invalid, await EditAsync(Guid.CreateVersion7(), edit));
        Assert.Equal(0, _documents.Saves);
    }

    [Fact]
    public async Task ABlankSummary_ClearsIt_AndANullTitleKeepsTheTitle()
    {
        var document = _documents.Add(_current.Id, isPrivate: false);
        await EditAsync(document.Id, Edit() with { Summary = "Alt." });

        Assert.Equal(ChangeOutcome.Changed, await EditAsync(document.Id, Edit() with { Summary = "  " }));

        Assert.Equal(("Brief", null), (document.Title, document.Summary));
    }

    [Fact]
    public async Task ARemovedAccount_ChangesNothing()
    {
        var document = _documents.Add(_current.Id, isPrivate: false);
        _current.Exists = false;

        Assert.Equal(ChangeOutcome.AccountGone, await EditAsync(document.Id, Edit()));
        Assert.Equal(0, _documents.Saves);
        Assert.Equal([AccountGone], _log.EventIds);
    }

    private static FieldsEdit Edit() => new(null, s_correspondent, null, null, null, null, null, []);

    private Task<ChangeOutcome> EditAsync(Guid id, FieldsEdit edit) =>
        new EditDocumentFields(_documents, _current, _log).EditAsync(id, edit, Ct);
}
