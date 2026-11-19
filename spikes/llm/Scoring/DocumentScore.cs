namespace LlmSpike.Scoring;

/// <summary>
/// Per-field verdict for one document. "Match" compares normalised names and accepts aliases.
/// "FromList" is only set when the expected value is an existing list entry: true means the model
/// returned exactly that spelling, so the worker could assign it without any fuzzy matching.
/// </summary>
public sealed record DocumentScore(
    string Id,
    bool ValidJson,
    bool CorrespondentMatch,
    bool? CorrespondentFromList,
    bool TypeMatch,
    bool? TypeFromList,
    bool Date,
    bool Amount,
    bool AmountAndCurrency,
    double TagRecall,
    double TagPrecision,
    bool TagHit,
    double TagsFromList,
    bool Title,
    bool Summary,
    IReadOnlyList<string> Misses);
