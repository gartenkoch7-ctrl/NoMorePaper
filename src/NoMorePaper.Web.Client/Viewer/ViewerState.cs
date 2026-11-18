namespace NoMorePaper.Web.Client.Viewer;

/// <summary>What the viewer shows once a page is rendered sharp: zoom, page and whether it has text.</summary>
public sealed record ViewerState(int ZoomPercent, int Page, int PageCount, bool HasText);
