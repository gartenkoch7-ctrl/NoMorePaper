namespace LlmSpike.Runs;

/// <summary>Settings of one measurement run, stored with the raw answers.</summary>
public sealed record RunHeader(
    string Label,
    string Model,
    string OllamaVersion,
    int ContextTokens,
    int Threads,
    int HeadChars,
    int TailChars,
    int Examples,
    bool ColdPrefix,
    string StartedUtc,
    long LoadedBytes);
