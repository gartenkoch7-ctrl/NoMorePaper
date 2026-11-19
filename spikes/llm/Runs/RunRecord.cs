using System.Text.Json.Serialization;

namespace LlmSpike.Runs;

/// <summary>Raw answer and timings for one document. Scoring happens later, from this record.</summary>
public sealed record RunRecord(
    string Id,
    int TextChars,
    int SentChars,
    int ExamplesSent,
    double WallSeconds,
    double LoadSeconds,
    double PromptSeconds,
    int PromptTokens,
    double EvalSeconds,
    int EvalTokens,
    string? DoneReason,
    string Content,
    string? Error)
{
    /// <summary>Time the worker would wait once the model is in memory.</summary>
    [JsonIgnore]
    public double WarmSeconds => Math.Max(0, this.WallSeconds - this.LoadSeconds);
}
