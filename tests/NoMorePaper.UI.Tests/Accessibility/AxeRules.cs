using Deque.AxeCore.Commons;

namespace NoMorePaper.UI.Tests.Accessibility;

/// <summary>The axe-core rule set every page must pass (AGENTS.md 2.5, ADR-0010).</summary>
public static class AxeRules
{
    /// <summary>WCAG AAA text contrast (7:1), not part of the AA tags, so enabled explicitly.</summary>
    public const string EnhancedContrastRule = "color-contrast-enhanced";

    /// <summary>
    /// WCAG 2.0/2.1/2.2 A and AA, axe best practices (landmarks, one H1, heading order, skip link
    /// target) and AAA text contrast.
    /// </summary>
    public static AxeRunOptions Strict() => new()
    {
        RunOnly = new RunOnlyOptions
        {
            Type = "tag",
            Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22a", "wcag22aa", "best-practice"],
        },
        Rules = new Dictionary<string, RuleOptions>
        {
            [EnhancedContrastRule] = new RuleOptions { Enabled = true },
        },
    };
}
