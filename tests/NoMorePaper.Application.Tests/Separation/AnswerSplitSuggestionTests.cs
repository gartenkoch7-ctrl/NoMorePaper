using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Separation;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Application.Tests.Separation;

/// <summary>Accepting and dismissing a split suggestion in "to review".</summary>
public sealed class AnswerSplitSuggestionTests
{
    private static readonly SplitSuggestion s_suggestion = new(
        [new PageRange(1, 2), new PageRange(3, 3), new PageRange(4, 6)], [5], [SplitReason.Blank, SplitReason.Salutation]);

    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingSplitRequester _splitter = new();
    private readonly RecordingLogger<AnswerSplitSuggestion> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Accept_SplitsAsSuggested_WithoutTheBlankPages_AndEndsTheSuggestion()
    {
        var document = Stack(_current.Id);

        Assert.Equal(ChangeOutcome.Changed, await Answer().AcceptAsync(document.Id, Ct));

        var (id, plan) = Assert.Single(_splitter.Requests);
        Assert.Equal(document.Id, id);
        Assert.Equal(["1,2", "3", "4,6"], plan.Parts.Select(part => string.Join(',', part.Steps.Select(step => step.SourcePage))));
        Assert.Null(document.SplitSuggestion);
        Assert.Equal(1, _documents.Saves);
        Assert.Contains(5710, _log.EventIds);
    }

    [Theory]
    [InlineData(ChangeOutcome.Conflict)]
    [InlineData(ChangeOutcome.Invalid)]
    public async Task Accept_KeepsTheSuggestion_AndIsAConflict_WhenTheSplitIsRefused(ChangeOutcome refusal)
    {
        var document = Stack(_current.Id);
        _splitter.Outcome = refusal;
        SplitSuggestion? whileAsked = null;
        _splitter.WhileAsked = () => whileAsked = document.SplitSuggestion;

        Assert.Equal(ChangeOutcome.Conflict, await Answer().AcceptAsync(document.Id, Ct));

        Assert.Equal(s_suggestion, whileAsked);
        Assert.Equal(s_suggestion, document.SplitSuggestion);
        Assert.Equal(0, _documents.Saves);
        Assert.DoesNotContain(5710, _log.EventIds);
    }

    [Fact]
    public async Task Accept_EndsTheSuggestion_OnlyAfterTheSplitIsQueued()
    {
        var document = Stack(_current.Id);
        SplitSuggestion? whileAsked = null;
        _splitter.WhileAsked = () => whileAsked = document.SplitSuggestion;

        Assert.Equal(ChangeOutcome.Changed, await Answer().AcceptAsync(document.Id, Ct));

        Assert.Equal(s_suggestion, whileAsked);
        Assert.Null(document.SplitSuggestion);
        Assert.Equal(1, _documents.Saves);
    }

    [Fact]
    public async Task Dismiss_EndsTheSuggestion_WithoutSplitting()
    {
        var document = Stack(_current.Id);

        Assert.Equal(ChangeOutcome.Changed, await Answer().DismissAsync(document.Id, Ct));

        Assert.Null(document.SplitSuggestion);
        Assert.Empty(_splitter.Requests);
        Assert.Equal(1, _documents.Saves);
        Assert.Contains(5711, _log.EventIds);
    }

    [Fact]
    public async Task BothAnswers_AreAConflict_WithoutSuggestion()
    {
        var document = _documents.Add(_current.Id, isPrivate: false);

        Assert.Equal(ChangeOutcome.Conflict, await Answer().AcceptAsync(document.Id, Ct));
        Assert.Equal(ChangeOutcome.Conflict, await Answer().DismissAsync(document.Id, Ct));
        Assert.Empty(_splitter.Requests);
    }

    [Fact]
    public async Task AnEditor_MayAnswer_OthersMayNot_AndPrivateStacksStayHidden()
    {
        var owner = Guid.CreateVersion7();
        var shared = Stack(owner);
        shared.SetEditors(owner, [_current.Id]);
        var foreign = Stack(Guid.CreateVersion7());
        var hidden = Stack(Guid.CreateVersion7(), isPrivate: true);

        Assert.Equal(ChangeOutcome.Changed, await Answer().DismissAsync(shared.Id, Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await Answer().AcceptAsync(foreign.Id, Ct));
        Assert.Equal(ChangeOutcome.NotFound, await Answer().AcceptAsync(hidden.Id, Ct));
        Assert.NotNull(foreign.SplitSuggestion);
        Assert.Empty(_splitter.Requests);
    }

    [Fact]
    public async Task ARemovedAccount_AnswersNothing()
    {
        var document = Stack(_current.Id);
        _current.Exists = false;

        Assert.Equal(ChangeOutcome.AccountGone, await Answer().AcceptAsync(document.Id, Ct));
        Assert.NotNull(document.SplitSuggestion);
    }

    private AnswerSplitSuggestion Answer() => new(_documents, _splitter, _current, _log);

    private Document Stack(Guid owner, bool isPrivate = false)
    {
        var document = _documents.Add(owner, isPrivate);
        document.SuggestSplit(s_suggestion);
        return document;
    }
}
