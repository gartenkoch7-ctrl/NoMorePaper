namespace NoMorePaper.Application.Classification;

/// <summary>
/// The fields an editor sends (<see cref="EditDocumentFields"/>), unchecked. Replaces all fields: a
/// <c>null</c> field is cleared, except <paramref name="Title"/>, where <c>null</c> keeps the title.
/// </summary>
/// <param name="Amount">The amount; needs <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code of <paramref name="Amount"/>.</param>
/// <param name="Summary">One or two sentences; empty or blank clears it.</param>
/// <param name="TagIds">All tags of the document; <c>null</c> is invalid, so tags are never removed by accident.</param>
public sealed record FieldsEdit(
    string? Title,
    Guid? CorrespondentId,
    Guid? DocumentTypeId,
    DateOnly? DocumentDate,
    decimal? Amount,
    string? Currency,
    string? Summary,
    IReadOnlyList<Guid>? TagIds);
