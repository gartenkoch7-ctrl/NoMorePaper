using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Design;

namespace NoMorePaper.Web.Client.Documents;

/// <summary>Maps the processing state of a document to the status building block. Pure logic.</summary>
public static class DocumentStatus
{
    public static StatusKind KindOf(DocumentProcessingState state) => state switch
    {
        DocumentProcessingState.Queued => StatusKind.Waiting,
        DocumentProcessingState.Processing => StatusKind.Processing,
        DocumentProcessingState.Done => StatusKind.Done,
        DocumentProcessingState.Failed => StatusKind.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown processing state."),
    };
}
