using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Server.Classification;

/// <summary>Maps the Application read models of fields and lists to the Contracts DTOs, and the route names of the lists. Pure logic.</summary>
internal static class ClassificationMapping
{
    /// <summary>The list of a route segment: <c>correspondents</c>, <c>document-types</c> or <c>tags</c>.</summary>
    public static ClassificationKind? KindOf(string segment) => segment.ToLowerInvariant() switch
    {
        "correspondents" => ClassificationKind.Correspondent,
        "document-types" => ClassificationKind.DocumentType,
        "tags" => ClassificationKind.Tag,
        _ => null,
    };

    public static EntryDto ToDto(this EntrySummary entry) => new(entry.Id, entry.Name);

    public static MoneyDto ToDto(this Money money) => new(money.Value, money.Currency);

    public static DocumentRecognitionState ToDto(this RecognitionState state) => state switch
    {
        RecognitionState.None => DocumentRecognitionState.None,
        RecognitionState.Automatic => DocumentRecognitionState.Automatic,
        RecognitionState.Confirmed => DocumentRecognitionState.Confirmed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown recognition state."),
    };

    /// <summary>The state a client sent; <c>null</c> for a number outside the enum, which the JSON converter lets through.</summary>
    public static RecognitionState? ToDomain(this DocumentRecognitionState state) => state switch
    {
        DocumentRecognitionState.None => RecognitionState.None,
        DocumentRecognitionState.Automatic => RecognitionState.Automatic,
        DocumentRecognitionState.Confirmed => RecognitionState.Confirmed,
        _ => null,
    };

    public static DocumentFieldsDto ToDto(this DocumentFieldsView view) =>
        new(
            view.Correspondent?.ToDto(),
            view.DocumentType?.ToDto(),
            view.DocumentDate,
            view.Amount?.ToDto(),
            view.Summary,
            [.. view.Tags.Select(tag => tag.ToDto())],
            view.Recognition.ToDto(),
            view.PossibleDuplicateOf,
            view.EditorIds,
            view.CanEdit);

    public static ReviewItemDto ToDto(this ReviewItem item) =>
        new(item.Id, item.Title, item.ReceivedAt, item.Fields.ToDto(), item.PossibleDuplicate?.ToDto());

    public static DuplicateCandidateDto ToDto(this DuplicateCandidate candidate) =>
        new(candidate.Id, candidate.Title, candidate.ReceivedAt, candidate.DocumentDate, candidate.Correspondent?.ToDto());

    /// <summary>The fields of a request, unchecked (the use case validates them), with the state the client saw.</summary>
    public static FieldsEdit ToEdit(this EditFieldsRequestDto request, RecognitionState seen) =>
        new(
            request.Title,
            request.CorrespondentId,
            request.DocumentTypeId,
            request.DocumentDate,
            request.Amount?.Value,
            request.Amount?.Currency,
            request.Summary,
            request.TagIds,
            seen);

    /// <summary>The label part of a list row or search hit: correspondent, type, date, amount, recognition.</summary>
    public static (EntryDto? Correspondent, EntryDto? DocumentType, DateOnly? DocumentDate, MoneyDto? Amount, DocumentRecognitionState Recognition)
        ToDto(this DocumentLabels labels) =>
        (labels.Correspondent?.ToDto(), labels.DocumentType?.ToDto(), labels.DocumentDate, labels.Amount?.ToDto(), labels.Recognition.ToDto());
}
