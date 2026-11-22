using Microsoft.Playwright;

namespace NoMorePaper.UI.Tests.Keyboard;

/// <summary>What has keyboard focus.</summary>
// Settable properties with a parameterless constructor: Playwright deserializes into it.
public sealed record FocusState
{
    public string Text { get; init; } = "";

    public string Id { get; init; } = "";

    public string ClassName { get; init; } = "";

    private const string Script = """
        () => {
          const el = document.activeElement;
          if (!el || el === document.body) {
            return { Text: '', Id: '', ClassName: '' };
          }
          return {
            Text: (el.innerText || '').trim(),
            Id: el.id || '',
            ClassName: String(el.className || ''),
          };
        }
        """;

    public static Task<FocusState> ReadAsync(IPage page) => page.EvaluateAsync<FocusState>(Script);
}
