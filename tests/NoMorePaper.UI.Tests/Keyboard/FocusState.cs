using Microsoft.Playwright;

namespace NoMorePaper.UI.Tests.Keyboard;

/// <summary>What has keyboard focus and how wide its visible focus indicator is.</summary>
// Settable properties with a parameterless constructor: Playwright deserializes into it.
public sealed record FocusState
{
    public string Text { get; init; } = "";

    public string Id { get; init; } = "";

    public string ClassName { get; init; } = "";

    public double IndicatorWidth { get; init; }

    // Indicator width = outline width (if drawn) or the largest box-shadow spread, in CSS px.
    private const string Script = """
        () => {
          const el = document.activeElement;
          if (!el || el === document.body) {
            return { Text: '', Id: '', ClassName: '', IndicatorWidth: 0 };
          }
          const style = getComputedStyle(el);
          const transparent = /rgba\([^)]*,\s*0\)$/.test(style.outlineColor);
          const outline = style.outlineStyle !== 'none' && !transparent
            ? (parseFloat(style.outlineWidth) || 0)
            : 0;
          const spreads = style.boxShadow === 'none' ? [] : style.boxShadow
            .replace(/rgba?\([^)]*\)/g, '')
            .split(',')
            .map(shadow => (shadow.match(/-?\d+(\.\d+)?px/g) || []).map(parseFloat)[3] || 0);
          return {
            Text: (el.innerText || '').trim(),
            Id: el.id || '',
            ClassName: String(el.className || ''),
            IndicatorWidth: Math.max(outline, ...spreads, 0),
          };
        }
        """;

    public static Task<FocusState> ReadAsync(IPage page) => page.EvaluateAsync<FocusState>(Script);
}
