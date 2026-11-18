using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>Uploads belong to the signed-in account; every read and change asks as that account.</summary>
public sealed class DocumentOwnershipTests
{
    private readonly FakeDocumentRepository _documents = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeCurrentAccount _current = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Receive_MakesTheSignedInAccountTheOwner(bool isPrivate)
    {
        var receive = new ReceiveDocument(_documents, _storage, TimeProvider.System, _current, NullLogger<ReceiveDocument>.Instance);

        await receive.ReceiveAsync("a.pdf", "application/pdf", new MemoryStream([1]), isPrivate, Ct);

        var document = Assert.Single(_documents.Added);
        Assert.Equal((_current.Id, isPrivate), (document.OwnerId, document.IsPrivate));
    }

    [Fact]
    public async Task Reads_AskAsTheSignedInAccount()
    {
        _storage.StoredBytes = [1];

        await new GetDocument(_documents, _current).GetAsync(Guid.CreateVersion7(), Ct);
        Assert.Equal(_current.Id, _documents.LastViewer);

        _current.Id = Guid.CreateVersion7();
        await new OpenDocumentFile(_documents, _storage, _current).OpenAsync(Guid.CreateVersion7(), DocumentFileKind.Original, Ct);
        Assert.Equal(_current.Id, _documents.LastViewer);
    }

    [Fact]
    public async Task SetPrivacy_PassesTheSignedInAccountAsOwner_AndLogsOnlyChanges()
    {
        var log = new RecordingLogger<SetDocumentPrivacy>();
        var privacy = new SetDocumentPrivacy(_documents, _current, log);
        var id = Guid.CreateVersion7();

        Assert.True(await privacy.SetAsync(id, isPrivate: true, Ct));
        Assert.Equal((id, _current.Id, true), _documents.LastPrivacy);

        _documents.PrivacyResult = false;
        Assert.False(await privacy.SetAsync(id, isPrivate: false, Ct));
        Assert.Equal([5120], log.EventIds);
    }
}
