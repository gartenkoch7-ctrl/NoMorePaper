using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Contracts.Documents;

/// <summary>One row of the document list.</summary>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it (and may change <paramref name="IsPrivate"/>).</param>
/// <param name="Correspondent">Who sent it; <c>null</c> when not set.</param>
/// <param name="DocumentType">What it is; <c>null</c> when not set.</param>
/// <param name="DocumentDate">Date printed on the document; <c>null</c> when not set.</param>
/// <param name="Amount">Amount with currency; <c>null</c> when not set.</param>
/// <param name="Recognition">Where the fields come from.</param>
/// <param name="Source">How it came in: <c>upload</c>, <c>camera</c>, <c>scanner-folder</c>, <c>mail</c> or <c>icloud</c> (as <c>DocumentFieldsDto.Source</c>).</param>
/// <param name="Excerpt">Summary or start of the recognized text, at most 160 characters plus an ellipsis; show as text only.</param>
/// <param name="Tags">Tags of the document, sorted by name; <c>null</c> when not loaded.</param>
/// <param name="EditedAt">When its newest edited version was made; <c>null</c> without one. Part of the thumbnail address, so an edit shows.</param>
public sealed record DocumentSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentProcessingState State,
    bool IsPrivate = false,
    bool IsMine = false,
    EntryDto? Correspondent = null,
    EntryDto? DocumentType = null,
    DateOnly? DocumentDate = null,
    MoneyDto? Amount = null,
    DocumentRecognitionState Recognition = DocumentRecognitionState.None,
    string Source = "upload",
    string? Excerpt = null,
    IReadOnlyList<EntryDto>? Tags = null,
    DateTimeOffset? EditedAt = null);
