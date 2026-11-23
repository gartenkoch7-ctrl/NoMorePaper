using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;

namespace NoMorePaper.Web.Client.Viewer;

/// <summary>
/// Builds the history of a document from what the API reports today. Only the arrival has a time; the
/// other steps are shown as the state they lead to, newest first. Pure, so it can be tested without a clock.
/// </summary>
public static class HistoryTimeline
{
    /// <returns>Newest first; the arrival is always last.</returns>
    public static IReadOnlyList<HistoryEntry> Build(
        DateTimeOffset receivedAt, DocumentProcessingState state, DocumentRecognitionState? recognition, string? source)
    {
        var entries = new List<HistoryEntry>();

        // Confirming is possible while the OCR still runs, so the fields entry does not depend on the state.
        switch (recognition)
        {
            case DocumentRecognitionState.Confirmed:
                entries.Add(new HistoryEntry(HistoryEntryKind.FieldsConfirmed, null, null));
                break;
            case DocumentRecognitionState.Automatic:
                entries.Add(new HistoryEntry(HistoryEntryKind.FieldsRecognized, null, null));
                break;
        }

        var stateKind = state switch
        {
            DocumentProcessingState.Queued => HistoryEntryKind.Queued,
            DocumentProcessingState.Processing => HistoryEntryKind.Processing,
            DocumentProcessingState.Done => HistoryEntryKind.TextRecognized,
            _ => HistoryEntryKind.Failed,
        };
        entries.Add(new HistoryEntry(stateKind, null, null));
        entries.Add(new HistoryEntry(HistoryEntryKind.Received, receivedAt, SourceKey(source)));
        return entries;
    }

    /// <returns>The text key of the entry title, e.g. <c>HistoryTextRecognized</c>.</returns>
    public static string TitleKey(HistoryEntryKind kind) => "History" + kind;

    /// <returns>The text key of a source as <see cref="DocumentFieldsDto.Source"/> names it, or <c>null</c> if unknown.</returns>
    public static string? SourceKey(string? source) => source switch
    {
        "upload" => "HistorySourceUpload",
        "camera" => "HistorySourceCamera",
        "scanner-folder" => "HistorySourceScannerFolder",
        "mail" => "HistorySourceMail",
        "icloud" => "HistorySourceICloud",
        _ => null,
    };
}
