using System.Data.Common;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>The intake rules with fakes; the same use case against Postgres is tested in Infrastructure.Tests.</summary>
public sealed class ReceiveDocumentTests
{
    private const int Received = 3200;
    private const int ReceiveFailed = 3201;
    private const int DiscardFailed = 3202;
    private const int KeptRecorded = 3203;
    private const int KeptUnverified = 3204;
    private const int DuplicateRefused = 5280;

    private static readonly DateTimeOffset s_now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDocumentRepository _documents = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly RecordingLogger<ReceiveDocument> _log = new();
    private readonly FakeCurrentAccount _current = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Receive_StoresTheCleanedName_AndRecordsAQueuedDocument()
    {
        var id = await ReceiveAsync("../x/Brief 1.pdf");

        var document = Assert.Single(_documents.Added);
        Assert.Equal((id, "Brief 1", s_now, ProcessingState.Queued), (document.Id, document.Title, document.ReceivedAt, document.State));
        var original = Assert.Single(document.Files);
        Assert.Equal(
            (DocumentFileKind.Original, "Brief 1.pdf", "application/pdf", 3L, FakeDocumentStorage.Sha256),
            (original.Kind, original.FileName, original.MediaType, original.SizeBytes, original.Sha256));
        Assert.Equal("Brief 1.pdf", _storage.Inbox[id].FileName);
        Assert.Equal([Received], _log.EventIds);
    }

    [Fact]
    public async Task Receive_CommitsWithoutTheCallersCancellation()
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        await Receive().ReceiveAsync("a.pdf", "application/pdf", new MemoryStream([1]), isPrivate: false, request.Token);

        Assert.False(_documents.AddToken?.CanBeCanceled);
    }

    [Fact]
    public async Task Receive_WhenStoringFails_DiscardsTheInbox_AndRethrows()
    {
        _storage.SaveFailure = new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => ReceiveAsync("a.pdf"));

        Assert.Single(_storage.Discarded);
        Assert.Empty(_documents.Added);
        Assert.Equal([ReceiveFailed], _log.EventIds);
    }

    [Fact]
    public async Task Receive_WhenCancelledWhileStoring_DiscardsTheInbox_WithoutLoggingAFailure()
    {
        _storage.SaveFailure = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => ReceiveAsync("a.pdf"));

        Assert.Single(_storage.Discarded);
        Assert.Empty(_log.EventIds);
    }

    [Fact]
    public async Task Receive_WhenTheCommitFails_AndNothingIsRecorded_DiscardsTheInbox()
    {
        _documents.AddFailure = new InvalidOperationException("refused");

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReceiveAsync("a.pdf"));

        Assert.Single(_storage.Discarded);
        Assert.Empty(_storage.Inbox);
        Assert.Equal([ReceiveFailed], _log.EventIds);
    }

    [Fact]
    public async Task Receive_WhenTheCommitReportsAFailureAfterItWasApplied_KeepsTheInbox()
    {
        _documents.AddFailure = new OperationCanceledException();
        _documents.RecordDespiteFailure = true;

        await Assert.ThrowsAsync<OperationCanceledException>(() => ReceiveAsync("a.pdf"));

        Assert.Empty(_storage.Discarded);
        Assert.Single(_storage.Inbox);
        Assert.Equal([KeptRecorded], _log.EventIds);
    }

    [Theory]
    [InlineData("db")]
    [InlineData("invalid")]
    [InlineData("timeout")]
    public async Task Receive_WhenTheRecordCannotBeChecked_KeepsTheInbox(string failure)
    {
        _documents.AddFailure = new InvalidOperationException("refused");
        _documents.ExistsFailure = failure switch
        {
            "db" => new UnreachableDatabaseException(),
            "invalid" => new InvalidOperationException("broken connection"),
            _ => new TimeoutException(),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReceiveAsync("a.pdf"));

        Assert.Empty(_storage.Discarded);
        Assert.Equal([ReceiveFailed, KeptUnverified], _log.EventIds);
    }

    [Fact]
    public async Task Receive_WhenDiscardingFails_LogsIt_AndRethrowsTheOriginalError()
    {
        _storage.SaveFailure = new IOException("disk full");
        _storage.DiscardFailure = new UnauthorizedAccessException();

        var error = await Assert.ThrowsAsync<IOException>(() => ReceiveAsync("a.pdf"));

        Assert.Equal("disk full", error.Message);
        Assert.Equal([ReceiveFailed, DiscardFailed], _log.EventIds);
    }

    [Fact]
    public async Task Receive_WhenTheUploaderMaySeeTheSameFile_KeepsNothing_AndNamesTheExistingDocument()
    {
        var existing = Guid.CreateVersion7();
        _documents.VisibleDuplicate = existing;

        var result = await Receive().ReceiveAsync("a.pdf", "application/pdf", new MemoryStream([1, 2, 3]), isPrivate: false, Ct);

        Assert.Equal(ReceiveResult.Duplicate(existing), result);
        Assert.Equal((_current.Id, FakeDocumentStorage.Sha256), _documents.LastHashLookup);
        Assert.Empty(_documents.Added);
        Assert.Empty(_storage.Inbox);
        Assert.Single(_storage.Discarded);
        Assert.Equal([DuplicateRefused], _log.EventIds);
    }

    private async Task<Guid> ReceiveAsync(string fileName, bool isPrivate = false) =>
        (await Receive().ReceiveAsync(fileName, "application/pdf", new MemoryStream([1, 2, 3]), isPrivate, Ct)).DocumentId
            ?? throw new InvalidOperationException("The signed-in account exists in these tests.");

    private ReceiveDocument Receive() => new(_documents, _storage, new FixedClock(s_now), _current, _log);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class UnreachableDatabaseException : DbException;
}
