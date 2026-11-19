namespace NoMorePaper.Application.Inputs;

/// <summary>Why an input did not get through; the page "Inputs" says each in plain words.</summary>
public enum InputProblemKind
{
    /// <summary>The input is not set up (no mailbox, no iCloud account).</summary>
    NotConfigured,

    /// <summary>Sign-in refused (wrong password, session gone).</summary>
    SignInFailed,

    /// <summary>iCloud needs a new code from a trusted device.</summary>
    CodeNeeded,

    /// <summary>Server or folder not reachable.</summary>
    Unreachable,

    /// <summary>The local LLM did not answer; mails stay untouched until it does.</summary>
    LlmUnavailable,

    /// <summary>The service asked to slow down (e.g. HTTP 429); the input pauses.</summary>
    Throttled,

    /// <summary>The service answered in an unexpected way; the input needs an update.</summary>
    ProtocolChanged,

    /// <summary>A scanner subfolder belongs to no account; its files stay where they are.</summary>
    UnknownFolder,

    /// <summary>Items failed repeatedly and were put aside (e.g. into the mailbox folder for failures).</summary>
    StuckItems,

    /// <summary>The run failed unexpectedly.</summary>
    Failed,
}
