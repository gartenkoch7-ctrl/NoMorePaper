using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Search;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Server.Classification;
using NoMorePaper.Server.Inputs;

namespace NoMorePaper.Server.Documents;

/// <summary>Maps the Application read models to the Contracts DTOs of the HTTP API. Pure logic.</summary>
internal static class DocumentMapping
{
    public static DocumentSummaryDto ToDto(this DocumentSummary summary)
    {
        var labels = summary.Labels.ToDto();
        return new(
            summary.Id, summary.Title, summary.ReceivedAt, summary.State.ToDto(), summary.IsPrivate, summary.IsMine,
            labels.Correspondent, labels.DocumentType, labels.DocumentDate, labels.Amount, labels.Recognition,
            summary.Source.ToWord(), summary.Excerpt, [.. summary.Tags.Select(tag => tag.ToDto())], summary.EditedAt);
    }

    public static DocumentCountsDto ToDto(this DocumentCounts counts) =>
        new(counts.Total, counts.AllTypes, [.. counts.ByType.Select(item => new DocumentTypeCountDto(item.Type.ToDto(), item.Count))]);

    public static DocumentDetailDto ToDto(this DocumentDetail detail) =>
        new(
            detail.Id,
            detail.Title,
            detail.ReceivedAt,
            detail.State.ToDto(),
            detail.FailureReason,
            detail.Original?.ToDto(),
            detail.Archive?.ToDto(),
            detail.IsPrivate,
            detail.IsMine,
            detail.Edited?.ToDto(),
            detail.EditedAt,
            detail.EditPending,
            detail.VersionPolicy.ToDto());

    public static VersionPolicyDto ToDto(this VersionPolicy policy) => policy switch
    {
        VersionPolicy.KeepBoth => VersionPolicyDto.KeepBoth,
        VersionPolicy.EditedOnly => VersionPolicyDto.EditedOnly,
        VersionPolicy.OriginalOnly => VersionPolicyDto.OriginalOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown version policy."),
    };

    /// <returns>The policy, or <c>null</c> for a value the API does not know.</returns>
    public static VersionPolicy? ToDomain(this VersionPolicyDto policy) => policy switch
    {
        VersionPolicyDto.KeepBoth => VersionPolicy.KeepBoth,
        VersionPolicyDto.EditedOnly => VersionPolicy.EditedOnly,
        VersionPolicyDto.OriginalOnly => VersionPolicy.OriginalOnly,
        _ => null,
    };

    public static SearchHitDto ToDto(this SearchHit hit)
    {
        var labels = hit.Labels.ToDto();
        return new(
            hit.Id, hit.Title, hit.ReceivedAt, hit.Snippet, hit.Rank, hit.IsPrivate, hit.IsMine,
            labels.Correspondent, labels.DocumentType, labels.DocumentDate, labels.Amount, labels.Recognition, hit.EditedAt);
    }

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
