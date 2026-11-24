using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Viewer;

namespace NoMorePaper.Web.Client.Pages;

// Loading and reloading of the document; the rest of the viewer lives in DocumentViewer.razor.cs.
public sealed partial class DocumentViewer
{
    protected override async Task OnParametersSetAsync()
    {
        if (Id == _loadedId)
        {
            return;
        }

        // The same component is reused when the route switches to another document.
        _loadedId = Id;
        _tab = FieldsTab;
        _openGen++;
        await Viewer.CloseAsync();
        (_detail, _fields, _error, _source, _state, _opened, _showFailed) = (null, null, null, null, null, false, false);
        // A note of the previous document must not be announced with the first view of the next one.
        _finishedNote = null;
        _originalColors = await Preferences.LoadOriginalColorsAsync();

        var result = await Api.GetAsync(Id);
        if (!result.IsSuccess || result.Value is null)
        {
            _error = result.Error;
            return;
        }

        ApplyDetail(result.Value);
    }

    // A finished edit (or the rebuild after "Nur Original"): fetch the document again so the viewer shows
    // the new archive. No loading state: polling triggers this without any user action, so focus, the open
    // tab and unsaved field edits must survive. The note is said with the first view announcement (a newer
    // announcement would replace it).
    private async Task ReloadAsync(bool withoutEditedVersion)
    {
        var id = Id;
        var result = await Api.GetAsync(id);
        if (!result.IsSuccess || result.Value is null || _disposed || id != Id)
        {
            return;
        }

        _openGen++;
        await Viewer.CloseAsync();
        // The close yields: the user may have switched documents or left meanwhile.
        if (_disposed || id != Id)
        {
            return;
        }

        _finishedNote = L[withoutEditedVersion ? "VerRebuildFinished" : "VerFinished"];
        (_state, _opened, _showFailed) = (null, false, false);
        ApplyDetail(result.Value);
        StateHasChanged();
    }

    private void ApplyDetail(DocumentDetailDto detail)
    {
        _detail = detail;
        _source = SourceOf(detail);
        if (_source is null)
        {
            Logger.DocumentNotViewable(detail.State);
        }
    }

    // Shows the archive PDF/A when there is one; a PDF original can be shown before processing.
    private static string? SourceOf(DocumentDetailDto detail) =>
        detail.Archive is not null ? DocumentLinks.Archive(detail.Id)
        : detail.Original?.MediaType == PdfMediaType ? DocumentLinks.Original(detail.Id)
        : null;
}
