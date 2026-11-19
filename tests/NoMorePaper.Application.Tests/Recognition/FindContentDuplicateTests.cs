using NoMorePaper.Application.Recognition;
using Xunit;

namespace NoMorePaper.Application.Tests.Recognition;

public sealed class FindContentDuplicateTests
{
    private static readonly string s_text = new('x', FindContentDuplicate.MinCharacters);

    private readonly FakeFinder _finder = new();
    private readonly RecordingLogger<FindContentDuplicate> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_MarksTheDocument_WhenASimilarOneIsFound()
    {
        var id = Guid.CreateVersion7();
        _finder.Found = Guid.CreateVersion7();

        await new FindContentDuplicate(_finder, _log).RunAsync(id, s_text, Ct);

        Assert.Equal((id, FindContentDuplicate.Threshold), _finder.Searched);
        Assert.Equal((id, _finder.Found.Value), _finder.Marked);
        Assert.Contains(5207, _log.EventIds);
    }

    [Fact]
    public async Task Run_SkipsShortTexts_AndLeavesUnfoundDocumentsAlone()
    {
        await new FindContentDuplicate(_finder, _log).RunAsync(Guid.CreateVersion7(), "  Seite 1  ", Ct);
        Assert.Null(_finder.Searched);

        await new FindContentDuplicate(_finder, _log).RunAsync(Guid.CreateVersion7(), s_text, Ct);
        Assert.NotNull(_finder.Searched);
        Assert.Null(_finder.Marked);
        Assert.Empty(_log.EventIds);
    }

    [Fact]
    public async Task Run_OnlyLogs_WhenTheCheckFails()
    {
        _finder.Failure = new TimeoutException("query on /data took long");

        await new FindContentDuplicate(_finder, _log).RunAsync(Guid.CreateVersion7(), s_text, Ct);

        Assert.Equal([5208], _log.EventIds);
        Assert.DoesNotContain(_log.Messages, message => message.Contains("/data", StringComparison.Ordinal));
    }

    private sealed class FakeFinder : IDuplicateFinder
    {
        public Guid? Found { get; set; }

        public Exception? Failure { get; set; }

        public (Guid, double)? Searched { get; private set; }

        public (Guid, Guid)? Marked { get; private set; }

        public Task<Guid?> FindSimilarAsync(Guid documentId, double threshold, CancellationToken cancellationToken)
        {
            Searched = (documentId, threshold);
            return Failure is null ? Task.FromResult(Found) : Task.FromException<Guid?>(Failure);
        }

        public Task<bool> MarkAsync(Guid documentId, Guid duplicateOf, CancellationToken cancellationToken)
        {
            Marked = (documentId, duplicateOf);
            return Task.FromResult(true);
        }
    }
}
