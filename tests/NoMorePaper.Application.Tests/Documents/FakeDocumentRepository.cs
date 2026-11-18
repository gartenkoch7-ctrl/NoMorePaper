using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>Documents in memory; each call can be made to fail.</summary>
internal sealed class FakeDocumentRepository : IDocumentRepository
{
    public List<Document> Added { get; } = [];

    /// <summary>Thrown by <see cref="AddReceivedAsync"/>; when <see cref="RecordDespiteFailure"/>, after recording.</summary>
    public Exception? AddFailure { get; set; }

    /// <summary>The commit reached the database although the caller hears of a failure.</summary>
    public bool RecordDespiteFailure { get; set; }

    public Exception? ExistsFailure { get; set; }

    public CancellationToken? AddToken { get; private set; }

    /// <summary>Page and page size of the last <see cref="ListAsync"/> call.</summary>
    public (int Page, int PageSize)? LastList { get; private set; }

    public DocumentDetail? Detail { get; set; }

    public Task AddReceivedAsync(Document document, CancellationToken cancellationToken)
    {
        AddToken = cancellationToken;
        if (AddFailure is not null && !RecordDespiteFailure)
        {
            throw AddFailure;
        }

        Added.Add(document);
        return AddFailure is null ? Task.CompletedTask : throw AddFailure;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        ExistsFailure is not null ? throw ExistsFailure : Task.FromResult(Added.Exists(document => document.Id == id));

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (page, pageSize);
        return Task.FromResult<IReadOnlyList<DocumentSummary>>([]);
    }

    public Task<DocumentDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Detail?.Id == id ? Detail : null);
}
