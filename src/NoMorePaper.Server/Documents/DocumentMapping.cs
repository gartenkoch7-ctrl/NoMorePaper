using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Search;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Server.Documents;

/// <summary>Maps the read models of Infrastructure to the Contracts DTOs of the HTTP API. Pure logic.</summary>
internal static class DocumentMapping
{
    public static DocumentSummaryDto ToDto(this DocumentSummary summary) =>
        new(summary.Id, summary.Title, summary.ReceivedAt, summary.State.ToDto());

    public static DocumentDetailDto ToDto(this DocumentDetail detail) =>
        new(
            detail.Id,
            detail.Title,
            detail.ReceivedAt,
            detail.State.ToDto(),
            detail.FailureReason,
            detail.Original.ToDto(),
            detail.Archive?.ToDto());

    public static SearchHitDto ToDto(this SearchHit hit) =>
        new(hit.Id, hit.Title, hit.ReceivedAt, hit.Snippet, hit.Rank);

    public static DocumentFileDto ToDto(this DocumentFile file) =>
        new(file.FileName, file.MediaType, file.SizeBytes, file.Sha256);

    private static DocumentProcessingState ToDto(this ProcessingState state) => state switch
    {
        ProcessingState.Queued => DocumentProcessingState.Queued,
        ProcessingState.Processing => DocumentProcessingState.Processing,
        ProcessingState.Done => DocumentProcessingState.Done,
        ProcessingState.Failed => DocumentProcessingState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown processing state."),
    };
}
