using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>List, detail and download over the ports; the SQL behind them is tested in Infrastructure.Tests.</summary>
public sealed class DocumentQueryTests
{
    private static readonly DocumentFile s_original = new(DocumentFileKind.Original, "scan.pdf", "application/pdf", 3, FakeDocumentStorage.Sha256);

    private readonly FakeDocumentRepository _documents = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeCurrentAccount _current = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1, 50, 1, 50)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-7, -1, 1, 1)]
    [InlineData(3, 100_000, 3, ListDocuments.MaxPageSize)]
    [InlineData(int.MaxValue, 100, int.MaxValue, 100)]
    public async Task List_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        await new ListDocuments(_documents, _current).ListAsync(page, pageSize, Ct);

        Assert.Equal((_current.Id, expectedPage, expectedPageSize), _documents.LastList);
    }

    [Fact]
    public async Task Get_ReturnsTheDocument_OrNullForAnUnknownId()
    {
        var detail = Detail(archive: null);
        _documents.Detail = detail;
        var get = new GetDocument(_documents, _current);

        Assert.Same(detail, await get.GetAsync(detail.Id, Ct));
        Assert.Null(await get.GetAsync(Guid.CreateVersion7(), Ct));
    }

    [Fact]
    public async Task OpenFile_ReturnsTheStoredBytes_WithTheFileOfTheRequestedKind()
    {
        var detail = Detail(archive: null);
        _documents.Detail = detail;
        _storage.StoredBytes = [1, 2, 3];

        var opened = await Open(detail.Id, DocumentFileKind.Original);

        Assert.NotNull(opened);
        await using var content = opened.Content;
        Assert.Same(s_original, opened.File);
        Assert.Equal(3, content.Length);
    }

    [Fact]
    public async Task OpenFile_ReturnsNull_ForAnUnknownDocument()
    {
        _storage.StoredBytes = [1];

        Assert.Null(await Open(Guid.CreateVersion7(), DocumentFileKind.Original));
    }

    [Fact]
    public async Task OpenFile_ReturnsNull_BeforeTheArchiveExists()
    {
        var detail = Detail(archive: null);
        _documents.Detail = detail;
        _storage.StoredBytes = [1];

        Assert.Null(await Open(detail.Id, DocumentFileKind.Archive));
    }

    [Fact]
    public async Task OpenFile_ReturnsNull_WhenTheBytesAreMissing()
    {
        var detail = Detail(archive: null);
        _documents.Detail = detail;

        Assert.Null(await Open(detail.Id, DocumentFileKind.Original));
    }

    private Task<DocumentFileContent?> Open(Guid id, DocumentFileKind kind) =>
        new OpenDocumentFile(_documents, _storage, _current).OpenAsync(id, kind, Ct);

    private static DocumentDetail Detail(DocumentFile? archive) =>
        new(Guid.CreateVersion7(), "Brief", DateTimeOffset.UnixEpoch, ProcessingState.Queued, null, s_original, archive, IsPrivate: false, IsMine: true);
}
