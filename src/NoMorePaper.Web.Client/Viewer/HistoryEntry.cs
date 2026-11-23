namespace NoMorePaper.Web.Client.Viewer;

/// <summary>One line of the history tab.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="At">When; only known for <see cref="HistoryEntryKind.Received"/>.</param>
/// <param name="SourceKey">Text key of the way it came in; only for <see cref="HistoryEntryKind.Received"/> with a known source.</param>
public sealed record HistoryEntry(HistoryEntryKind Kind, DateTimeOffset? At, string? SourceKey);
