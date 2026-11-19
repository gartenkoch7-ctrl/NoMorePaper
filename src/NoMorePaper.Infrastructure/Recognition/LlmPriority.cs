namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>Who asks the local LLM through the <see cref="LlmGate"/>; recognition goes first.</summary>
public enum LlmPriority
{
    /// <summary>Recognition of a document: holds a job with a lock, so it never waits behind mails.</summary>
    Recognition,

    /// <summary>The spam and note check of a mail: waits while any recognition waits.</summary>
    Mail,
}
