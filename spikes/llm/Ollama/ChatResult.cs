namespace LlmSpike.Ollama;

/// <summary>Answer and timings of one <c>/api/chat</c> call (durations in seconds).</summary>
public sealed record ChatResult(
    string Content,
    double WallSeconds,
    double LoadSeconds,
    double PromptSeconds,
    int PromptTokens,
    double EvalSeconds,
    int EvalTokens,
    string? DoneReason);
