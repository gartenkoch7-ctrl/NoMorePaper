using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Classification;

/// <summary>
/// Body for editing the fields of a document. Replaces all fields: a <c>null</c> field is cleared,
/// except <paramref name="Title"/>, where <c>null</c> keeps the current title. The fields count as confirmed.
/// </summary>
/// <param name="DocumentDate">Date printed on the document.</param>
/// <param name="Summary">One or two sentences about the content.</param>
/// <param name="TagIds">All tags of the document; required, so a request without it does not silently remove them.</param>
public sealed record EditFieldsRequestDto(
    string? Title,
    Guid? CorrespondentId,
    Guid? DocumentTypeId,
    DateOnly? DocumentDate,
    MoneyDto? Amount,
    string? Summary,
    [property: JsonRequired] IReadOnlyList<Guid> TagIds);
