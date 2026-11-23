using Microsoft.AspNetCore.Components.Web;
using NoMorePaper.Web.Client.Editing;

namespace NoMorePaper.Web.Client.Pages;

// What a button or key on a page card does to the model, what is announced and where focus goes.
public sealed partial class PageEditor
{
    // Cropped images by the key the model holds; filled by the crop step. Sent with "Speichern" as page-<n>.
    private readonly Dictionary<string, byte[]> _crops = [];

    // "Zuschneiden" and the automatic crop (P2c); the cards keep the switch so they can be turned off again.
    private static bool CropEnabled => true;

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
            case PageAction.Crop or PageAction.CropByHand:
                await StartCropAsync(page, automatic: false);
                break;
            case PageAction.AutoCrop:
                await StartCropAsync(page, automatic: true);
                break;
            case PageAction.CropApply:
                await ApplyOpenCropAsync();
                break;
            case PageAction.CropCancel:
                await CancelCropAsync();
                break;
            case PageAction.ClearCrop:
                await ClearCropAsync(index, page);
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
        if (page == _cropPage)
        {
            // A deleted page has no frame to set.
            await CloseCropAsync();
        }

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

        _focus.On(SplitFocus(index, page));
        // A point that only closes deleted pages divides nothing; saying "gesetzt" alone would be wrong.
        var message = !set ? "PeAnnSplitRemoved" : Model.EffectiveSplitsAfter.Contains(index) ? "PeAnnSplitSet" : "PeAnnSplitIdle";
        await Say(L[message, Model.NumberOf(index)]);
    }

    // The "Trennstelle entfernen" button is gone afterwards; focus goes to the "Hier teilen" button of the card above.
    private async Task RemoveSplitAsync(int index)
    {
        if (!Model.ToggleSplitAfter(index))
        {
            return;
        }

        _focus.On(SplitFocus(index, Model.Entries[index].SourcePage));
        await Say(L["PeAnnSplitRemoved", Model.NumberOf(index)]);
    }

    // A deleted card has no "Hier teilen" button, so its heading gets the focus.
    private string SplitFocus(int index, int page)
        => Model.Entries[index].Deleted ? PageEditorIds.Heading(page) : PageEditorIds.Button(page, "split");

    private async Task OnListKeyDown(KeyboardEventArgs e)
    {
        // While a frame is open its handles have the keys; undo would change the page under it.
        if (_saved != SavedKind.None || Cropping || !(e.CtrlKey || e.MetaKey) || e.AltKey || !string.Equals(e.Key, "z", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Model.CanUndo)
        {
            await Say(L["PeAnnNothingToUndo"]);
            return;
        }

        // The card the undone change touched; the button that had focus may be gone.
        if (Model.Undo() is { } page && Model.Entries.Any(entry => entry.SourcePage == page))
        {
            _focus.On(PageEditorIds.Heading(page));
        }

        await Say(L["PeAnnUndone"]);
    }
}
