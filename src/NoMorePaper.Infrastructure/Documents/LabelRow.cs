using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>The label columns of <see cref="DocumentFilterSql.LabelColumns"/>; property names equal the column aliases.</summary>
internal abstract class LabelRow
{
    public Guid? CorrespondentId { get; set; }

    public string? CorrespondentName { get; set; }

    public Guid? DocumentTypeId { get; set; }

    public string? DocumentTypeName { get; set; }

    public DateOnly? DocumentDate { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string Recognition { get; set; } = nameof(RecognitionState.None);

    public DocumentLabels ToLabels() => new(
        CorrespondentId is { } correspondent ? new EntrySummary(correspondent, CorrespondentName ?? string.Empty) : null,
        DocumentTypeId is { } type ? new EntrySummary(type, DocumentTypeName ?? string.Empty) : null,
        DocumentDate,
        Amount is { } amount && Currency is { } currency ? new Money(amount, currency) : null,
        Enum.Parse<RecognitionState>(Recognition));
}
