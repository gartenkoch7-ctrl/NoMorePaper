namespace NoMorePaper.Web.Client.Inputs;

/// <summary>What the page shows for one input row; always with symbol and word, never colour alone.</summary>
public enum InputRowState
{
    /// <summary>The input has not run yet.</summary>
    NeverRun,

    /// <summary>The last run got through.</summary>
    Running,

    /// <summary>The input waits for its sign-in details.</summary>
    NotSetUp,

    /// <summary>The last run failed; waiting may help.</summary>
    Problem,

    /// <summary>The last run failed and somebody has to act.</summary>
    NeedsAttention,
}
