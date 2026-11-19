namespace NoMorePaper.Contracts.Classification;

/// <summary>The fields of a document as the signed-in account sees them.</summary>
/// <param name="DocumentDate">Date printed on the document (not when it was received).</param>
/// <param name="Summary">One or two sentences about the content.</param>
/// <param name="PossibleDuplicateOf">
/// A document with (nearly) the same content; <c>null</c> when there is none or the signed-in account
/// may not see it.
/// </param>
/// <param name="EditorIds">Members besides the owner who may edit the fields.</param>
/// <param name="CanEdit">The signed-in account may edit and confirm the fields.</param>
public sealed record DocumentFieldsDto(
    EntryDto? Correspondent,
    EntryDto? DocumentType,
    DateOnly? DocumentDate,
    MoneyDto? Amount,
    string? Summary,
    IReadOnlyList<EntryDto> Tags,
    DocumentRecognitionState Recognition,
    Guid? PossibleDuplicateOf,
    IReadOnlyList<Guid> EditorIds,
    bool CanEdit);
