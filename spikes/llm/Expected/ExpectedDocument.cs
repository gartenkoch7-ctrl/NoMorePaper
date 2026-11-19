namespace LlmSpike.Expected;

/// <summary>
/// Known target values of one sample. Aliases are more specific but still acceptable names;
/// a tag entry may list alternatives separated by '|' (for example "Auto|Kfz").
/// </summary>
public sealed record ExpectedDocument(
    string Id,
    string Language,
    string Correspondent,
    IReadOnlyList<string> CorrespondentAliases,
    string DocumentType,
    IReadOnlyList<string> DocumentTypeAliases,
    string? Date,
    decimal? Amount,
    string? Currency,
    IReadOnlyList<decimal?> AmountAlternatives,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> TagsAccepted,
    IReadOnlyList<string> TitleKeywords,
    IReadOnlyList<string> SummaryKeywords,
    string Title,
    string Summary);
