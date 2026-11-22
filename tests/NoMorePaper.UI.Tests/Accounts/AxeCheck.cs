using System.Globalization;
using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Accessibility;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The axe assertion every account page test uses (same rules as the other pages).</summary>
internal static class AxeCheck
{
    public static async Task NoViolationsAsync(IPage page)
    {
        // The header is sticky from 1024 px and covers rows above the focus; axe's target-size rule
        // would flag that covered content, depending on where focus scrolled to. Check from the top.
        await page.EvaluateAsync("() => window.scrollTo(0, 0)");
        var result = await page.RunAxe(AxeRules.Strict());

        Assert.True(result.Violations.Length == 0, Describe(result.Violations));
    }

    private static string Describe(AxeResultItem[] violations)
    {
        var text = new StringBuilder();
        foreach (var violation in violations)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{violation.Id} ({violation.Impact}): {violation.Help}");
            foreach (var node in violation.Nodes)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"  {node.Target}: {node.Html}");
            }
        }

        return text.ToString();
    }
}
