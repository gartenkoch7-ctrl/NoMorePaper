using Microsoft.AspNetCore.Components.Web;
using NoMorePaper.Web.Client.Editing;

namespace NoMorePaper.Web.Client.Pages;

// What a button or key on a page card does to the model, what is announced and where focus goes.
public sealed partial class PageEditor
{
    // Cropped images by the key the model holds; filled by the crop step. Sent with "Speichern" as page-<n>.
    private readonly Dictionary<string, byte[]> _crops = [];

    // Card the last action was on; Strg+Z puts focus back on it, because the button that had focus may be gone.
    private int _lastPage;

    // "Zuschneiden" and the automatic crop are switched on once the crop step exists.
    private static bool CropEnabled => false;

    private IReadOnlyList<(int Page, byte[] Jpeg)> CropImages() =>
    [
        .. Model.Entries
            .Where(entry => !entry.Deleted && entry.ReplacedJpegKey is not null && _crops.ContainsKey(entry.ReplacedJpegKey))
            .Select(entry => (entry.SourcePage, _crops[entry.ReplacedJpegKey!])),
    ];

    private async Task OnCardActionAsync(int index, PageAction action)
    {
        if (_saved != SavedKind.None || index < 0 || index >= Model.Entries.Count)
        {
            return;
        }

        var page = Model.Entries[index].SourcePage;
        _lastPage = page;
        switch (action)
        {
            case PageAction.RotateRight:
                Model.RotateRight(index);
                await StayAsync(page, "rr", "PeAnnRotatedRight", Model.NumberOf(index));
                break;
            case PageAction.RotateLeft:
                Model.RotateLeft(index);
                await StayAsync(page, "rl", "PeAnnRotatedLeft", Model.NumberOf(index));
                break;
            case PageAction.MoveUp:
                await MovedAsync(page, Model.MoveUp(index), "up", up: true);
                break;
            case PageAction.MoveDown:
                await MovedAsync(page, Model.MoveDown(index), "down", up: false);
                break;
            case PageAction.ToggleDelete:
                await ToggleDeleteAsync(index, page);
                break;
            case PageAction.ToggleSplit:
                await ToggleSplitAsync(index, page);
                break;
        }
    }

    private async Task StayAsync(int page, string button, string messageKey, int number)
    {
        _focus.On(PageEditorIds.Button(page, button));
        await Say(L[messageKey, number]);
    }

    // After a move focus stays on the same button of the moved card; at the edge that button is gone, so the heading gets it.
    private async Task MovedAsync(int page, int newIndex, string button, bool up)
    {
        if (newIndex < 0)
        {
            return;
        }

        var more = up ? Model.CanMoveUp(newIndex) : Model.CanMoveDown(newIndex);
        _focus.On(more ? PageEditorIds.Button(page, button) : PageEditorIds.Heading(page));
        await Say(L["PeAnnMoved", Model.NumberOf(newIndex), Model.KeptCount]);
    }

    private async Task ToggleDeleteAsync(int index, int page)
    {
        var number = Model.NumberOf(index);
        var wasDeleted = Model.Entries[index].Deleted;
        Model.ToggleDelete(index);
        _focus.On(PageEditorIds.Button(page, "del"));
        await Say(wasDeleted ? L["PeAnnUndeleted", Model.NumberOf(index)] : L["PeAnnDeleted", number]);
    }

    private async Task ToggleSplitAsync(int index, int page)
    {
        var set = !Model.SplitsAfter.Contains(index);
        if (!Model.ToggleSplitAfter(index))
        {
            return;
        }

        _focus.On(PageEditorIds.Button(page, "split"));
        await Say(L[set ? "PeAnnSplitSet" : "PeAnnSplitRemoved", Model.NumberOf(index)]);
    }

    // The "Trennstelle entfernen" button is gone afterwards; focus goes to the "Hier teilen" button of the card above.
    private async Task RemoveSplitAsync(int index)
    {
        if (!Model.ToggleSplitAfter(index))
        {
            return;
        }

        _focus.On(PageEditorIds.Button(Model.Entries[index].SourcePage, "split"));
        await Say(L["PeAnnSplitRemoved", Model.NumberOf(index)]);
    }

    private async Task OnListKeyDown(KeyboardEventArgs e)
    {
        if (_saved != SavedKind.None || !(e.CtrlKey || e.MetaKey) || e.AltKey || !string.Equals(e.Key, "z", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Model.Undo())
        {
            await Say(L["PeAnnNothingToUndo"]);
            return;
        }

        if (_lastPage > 0 && Model.Entries.Any(entry => entry.SourcePage == _lastPage))
        {
            _focus.On(PageEditorIds.Heading(_lastPage));
        }

        await Say(L["PeAnnUndone"]);
    }
}
