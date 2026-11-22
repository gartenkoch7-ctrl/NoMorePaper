using Deque.AxeCore.Commons;

namespace NoMorePaper.UI.Tests.Accessibility;

/// <summary>The axe-core rule set every page must pass (AGENTS.md 2.5, ADR-0010, ADR-0026).</summary>
public static class AxeRules
{
    /// <summary>
    /// WCAG 2.0/2.1/2.2 A and AA (text contrast 4.5:1) and axe best practices (landmarks, one H1,
    /// heading order, skip link target). AAA contrast (7:1) is not checked since M4u: the approved
    /// Stitch design system decides the colours, and some of its secondary greys stay below 7:1.
    /// </summary>
    public static AxeRunOptions Strict() => new()
    {
        RunOnly = new RunOnlyOptions
        {
            Type = "tag",
            Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22a", "wcag22aa", "best-practice"],
        },
    };
}
