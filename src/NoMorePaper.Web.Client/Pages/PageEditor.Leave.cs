using Microsoft.AspNetCore.Components.Routing;
using NoMorePaper.Web.Client.Editing;

namespace NoMorePaper.Web.Client.Pages;

// Leaving the page with unsaved changes (plan F2): the browser asks on reload or close, the page asks itself
// on a link inside the app, with the same in-page question as "Alles verwerfen".
public sealed partial class PageEditor
{
    private string? _leaveTarget;
    private bool _leaving;

    private bool Guarded => Model.IsDirty && _saved == SavedKind.None;

    // Asked for every link inside the app; the page then shows its own question instead of changing the page.
    private Task OnBeforeNavigationAsync(LocationChangingContext context)
    {
        if (Guarded && !_leaving)
        {
            context.PreventNavigation();
            _leaveTarget = context.TargetLocation;
        }

        return Task.CompletedTask;
    }

    private void Leave()
    {
        var target = _leaveTarget;
        _leaveTarget = null;
        _leaving = true;
        if (target is not null)
        {
            Navigation.NavigateTo(target);
        }
    }

    // The link that was followed lies outside the page (navigation, breadcrumb); the heading is the safe place.
    private void CancelLeave()
    {
        _leaveTarget = null;
        _focus.On(PageEditorIds.Heading1);
    }
}
