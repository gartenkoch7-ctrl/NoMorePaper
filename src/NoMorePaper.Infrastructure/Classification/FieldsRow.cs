using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>The columns of a document that its fields view needs; never the recognized text.</summary>
internal sealed record FieldsRow(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    Guid? OwnerId,
    bool IsPrivate,
    Guid? CorrespondentId,
    Guid? DocumentTypeId,
    DateOnly? DocumentDate,
    Money? Amount,
    string? Summary,
    RecognitionState Recognition,
    Guid? PossibleDuplicateOf,
    List<Guid> TagIds,
    List<Guid> EditorIds)
{
    /// <summary><c>Document.CanEdit</c> on the loaded columns.</summary>
    public bool CanEdit(Guid accountId) =>
        OwnerId is { } owner && (owner == accountId || (!IsPrivate && EditorIds.Contains(accountId)));
}
