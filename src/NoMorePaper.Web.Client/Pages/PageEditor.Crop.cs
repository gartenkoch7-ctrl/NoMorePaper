using Microsoft.AspNetCore.Components;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Editing;

namespace NoMorePaper.Web.Client.Pages;

// Cropping and straightening a page (ADR-0025): the frame is set in the card, the browser turns it into a JPEG
// that replaces the page when "Speichern" sends the plan. One frame is open at a time.
public sealed partial class PageEditor : IAsyncDisposable
{
    // The frames of the applied crops by the key of their image, for the preview.
    private readonly Dictionary<string, CropShape> _cropShapes = [];
    private CropShape? _cropShape;
    private string? _cropNotice;
    private int _cropPage;
    private int _cropNoticePage;
    private int _cropCount;
    private bool _cropNotFound;
    private bool _cropBusy;

    [Inject] private CropInterop Cropper { get; set; } = default!;

    [Inject] private TimeProvider Clock { get; set; } = default!;

    private bool Cropping => _cropPage > 0;

    public async ValueTask DisposeAsync()
    {
        CancelCornerSay();
        await Cropper.ReleaseAsync();
    }

    private CropShape? CropOf(PageEntry entry) => _saved == SavedKind.None && entry.SourcePage == _cropPage ? _cropShape : null;

    private CropShape? AppliedOf(PageEntry entry) =>
        entry.ReplacedJpegKey is { } key && _cropShapes.TryGetValue(key, out var shape) ? shape : null;

    private string? CropNoticeOf(PageEntry entry) => entry.SourcePage == _cropNoticePage ? _cropNotice : null;

    // Draws the page and looks for the sheet; then either opens the frame there or (automatic) applies it at once.
    private async Task StartCropAsync(int page, bool automatic)
    {
        if (_cropBusy)
        {
            return;
        }

        await CloseCropAsync();
        _cropNotice = null;
        _cropBusy = true;
        await Say(L["CropWorking"]);
        var found = await Cropper.DetectAsync(PreviewSource, page, OnDragAsync);
        _cropBusy = false;
        var index = IndexOf(page);
        if (found.Shape is not { } shape || index < 0 || Model.Entries[index].Deleted || _saved != SavedKind.None)
        {
            await Cropper.ReleaseAsync();
            if (found.Error is { } error)
            {
                Fail(page, error);
            }

            return;
        }

        Logger.PageCropDetected(found.ElapsedMs, found.Found);
        if (automatic && !found.Found)
        {
            await Cropper.ReleaseAsync();
            ShowCropNotice(page, L["CropNotFound"], notFound: true);
            await Say(L["CropNotFound"]);
        }
        else if (automatic)
        {
            await ApplyAsync(page, shape, automatic: true);
        }
        else
        {
            _cropPage = page;
            _cropShape = shape;
            _status?.Clear();
            _focus.On(PageEditorIds.Corner(page, CropCorner.TopLeft));
        }
    }

    private async Task ApplyOpenCropAsync()
    {
        if (!Cropping || _cropBusy || _cropShape is not { } shape)
        {
            return;
        }

        var page = _cropPage;
        CancelCornerSay();
        _cropPage = 0;
        _cropShape = null;
        await ApplyAsync(page, shape, automatic: false);
    }

    private async Task ApplyAsync(int page, CropShape shape, bool automatic)
    {
        _cropBusy = true;
        var jpeg = await Cropper.ApplyAsync(shape.Quad);
        _cropBusy = false;
        var index = IndexOf(page);
        _focus.On(PageEditorIds.Button(page, automatic ? "auto" : "crop"));
        if (jpeg is null)
        {
            Fail(page, "apply");
            return;
        }

        if (index < 0 || Model.Entries[index].Deleted || _saved != SavedKind.None)
        {
            return;
        }

        // A new key for every crop: undo brings an earlier one back, so its image stays until the page is left.
        var key = $"crop-{page}-{++_cropCount}";
        _crops[key] = jpeg;
        _cropShapes[key] = shape;
        var text = Describe(shape.Measure(Model.Entries[index].Rotation));
        Model.Replace(index, key, text);
        await Say(automatic ? text : $"{L["CropApplied", Model.NumberOf(index)]} {text}");
    }

    private async Task CancelCropAsync()
    {
        var page = _cropPage;
        if (page == 0)
        {
            return;
        }

        await CloseCropAsync();
        _focus.On(PageEditorIds.Button(page, "crop"));
        await Say(L["CropCancelled"]);
    }

    private async Task ClearCropAsync(int index, int page)
    {
        Model.ClearCrop(index);
        _focus.On(PageEditorIds.Button(page, "crop"));
        await Say(L["PeAnnCropCleared", Model.NumberOf(index)]);
    }

    private async Task CloseCropAsync()
    {
        if (!Cropping)
        {
            return;
        }

        CancelCornerSay();
        _cropPage = 0;
        _cropShape = null;
        await Cropper.ReleaseAsync();
    }

    // "Alles verwerfen": no frame, no image and no notice stays.
    private async Task ResetCropsAsync()
    {
        await CloseCropAsync();
        _crops.Clear();
        _cropShapes.Clear();
        _cropNotice = null;
    }

    private Task OnCropChangeAsync((CropQuad Quad, CropCorner Corner) change)
    {
        if (_cropShape is { } shape)
        {
            _cropShape = shape with { Quad = change.Quad };
            SayCornerLater(change.Corner);
        }

        return Task.CompletedTask;
    }

    private Task OnDragAsync(CropCorner corner, CropPoint point, bool done) => InvokeAsync(() =>
    {
        if (_cropShape is not { } shape)
        {
            return;
        }

        _cropShape = shape with { Quad = CropGeometry.MoveTo(shape.Quad, RotationOf(_cropPage), corner, point) };
        if (done)
        {
            SayCornerLater(corner);
        }

        StateHasChanged();
    });

    private int IndexOf(int page)
    {
        for (var i = 0; i < Model.Entries.Count; i++)
        {
            if (Model.Entries[i].SourcePage == page)
            {
                return i;
            }
        }

        return -1;
    }

    private PageRotationDto RotationOf(int page) => IndexOf(page) is var index and >= 0 ? Model.Entries[index].Rotation : PageRotationDto.None;
}
