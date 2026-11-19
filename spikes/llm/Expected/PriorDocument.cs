namespace LlmSpike.Expected;

/// <summary>An earlier, user-confirmed document; used as a learning example for the same correspondent.</summary>
public sealed record PriorDocument(
    string Correspondent,
    string DocumentType,
    IReadOnlyList<string> Tags,
    string Title);
