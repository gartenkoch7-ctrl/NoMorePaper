namespace LlmSpike.Expected;

/// <summary>The simulated lists of a small household that the model is asked to reuse.</summary>
public sealed record Household(
    IReadOnlyList<string> Correspondents,
    IReadOnlyList<string> DocumentTypes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PriorDocument> PriorDocuments);
