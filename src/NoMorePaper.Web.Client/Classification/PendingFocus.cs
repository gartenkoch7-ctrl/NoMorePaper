using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>
/// Moves focus to an element by id once it is rendered. After an action the element that had focus
/// is often gone (a confirmed item leaves the list); a screen reader user must land somewhere
/// sensible instead of at the top of the page.
/// </summary>
public sealed class PendingFocus(IJSRuntime js)
{
    private string? _id;

    /// <summary>Asks for focus on the element with <paramref name="id"/> after the next render.</summary>
    public void On(string id) => _id = id;

    /// <summary>Call from <c>OnAfterRenderAsync</c>.</summary>
    public async Task ApplyAsync()
    {
        if (_id is not { } id)
        {
            return;
        }

        _id = null;
        await js.InvokeVoidAsync("nmpForms.focusById", id);
    }
}
