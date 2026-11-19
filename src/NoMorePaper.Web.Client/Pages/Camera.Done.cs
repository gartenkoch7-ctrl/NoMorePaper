namespace NoMorePaper.Web.Client.Pages;

/// <summary>"Fertig" on <c>/kamera</c>: the pages taken become one document.</summary>
public sealed partial class Camera
{
    private async Task DoneAsync()
    {
        _uploading = true;
        var count = _pages;
        await AnnounceAsync(L["CamUploading", count]);
        var jpegs = new List<byte[]>(count);
        for (var i = 0; i < count; i++)
        {
            jpegs.Add(await Interop.PageAsync(i));
        }

        // Unchanged switch: no choice is sent, the server takes the account's setting (also when it did not load here).
        var isPrivate = _privacy?.Effective;
        var result = await Upload.UploadPagesAsync(jpegs, _privacy?.Choice, _takenAt ?? Time.GetLocalNow());
        _uploading = false;
        _focusCapture = _running;
        if (result.Value is null && !result.IsDuplicate)
        {
            // "Fertig" was disabled during the upload and lost the focus; it is the way to try again.
            (_focusCapture, _focusDone) = (false, true);
            await AlertAsync(L["CamUploadFailed", result.Error ?? string.Empty]);
            return;
        }

        // Only the pages sent are dropped: the camera may have taken the next one meanwhile.
        _pages = await Interop.ClearAsync(count);
        _takenAt = _pages == 0 ? null : Time.GetLocalNow();
        _saved = result.Value is { } saved
            ? new SavedDocument(saved.Id, SavedText(isPrivate, count), false)
            : new SavedDocument(result.ExistingId, result.Error ?? string.Empty, true);
        _focusSaved = !_running;
        await AnnounceAsync(_saved.Message);
    }

    private string SavedText(bool? isPrivate, int count) => isPrivate switch
    {
        true => L["CamSavedPrivate", count],
        false => L["CamSavedShared", count],
        null => L["CamSavedByAccount", count],
    };

    private sealed record SavedDocument(Guid? Id, string Message, bool IsDuplicate);
}
