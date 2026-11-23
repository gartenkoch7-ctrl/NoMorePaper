using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Design;
using NoMorePaper.Web.Client.Documents;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>The follow-up of documents in progress: asks again every 3 seconds and announces state changes.</summary>
public sealed partial class Documents
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(3);

    private Dictionary<Guid, DocumentProcessingState> _lastStates = [];
    private bool _polling;

    private bool InProgress => _documents.Any(d => d.State is DocumentProcessingState.Queued or DocumentProcessingState.Processing);

    // Polls only while something is queued or processing; ends on its own when all is settled.
    private async Task PollAsync()
    {
        // A filter change starts polling again; one loop is enough.
        if (_polling || !InProgress)
        {
            return;
        }

        _polling = true;
        Logger.ListPollingStarted();
        using var timer = new PeriodicTimer(s_pollInterval);
        try
        {
            while (InProgress && await timer.WaitForNextTickAsync(_cts.Token))
            {
                var before = _lastStates;
                await LoadAsync(announceChanges: true);
                if (StateChanged(before))
                {
                    // A finished document can change the type counts of the filter chips.
                    await LoadCountsAsync();
                }

                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        finally
        {
            _polling = false;
        }
    }

    private bool StateChanged(Dictionary<Guid, DocumentProcessingState> before) =>
        _documents.Any(d => before.TryGetValue(d.Id, out var state) && state != d.State);

    // All changes of one poll in one message: one announcement per document would cut off the one
    // before it, so only the last change would be heard.
    private async Task AnnounceChangesAsync()
    {
        var changes = new List<string>();
        foreach (var document in _documents)
        {
            if (_lastStates.TryGetValue(document.Id, out var before) && before != document.State)
            {
                var state = document.State.ToString();
                Logger.DocumentStateChanged(state);
                changes.Add(L["StateChanged", document.Title, L[StatusKeys.For(DocumentStatus.KindOf(document.State))]]);
            }
        }

        if (changes.Count > 0 && _changes is not null)
        {
            await _changes.Announce(string.Join(". ", changes));
        }
    }

    public void Dispose()
    {
        // Not disposed: a poll loop may still read the token while the page is being torn down.
        _cts.Cancel();
    }
}
