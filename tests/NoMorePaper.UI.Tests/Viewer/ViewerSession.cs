using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>A browser session showing the viewer for the mocked document <see cref="Id"/>.</summary>
internal sealed class ViewerSession(BrowserSession session, Guid id, int[] textRequests) : IAsyncDisposable
{
    public IPage Page => session.Page;

    public Guid Id { get; } = id;

    /// <summary>How often the page asked for the recognized text (it may only do so once the tab is opened).</summary>
    public int TextRequests => Volatile.Read(ref textRequests[0]);

    public ValueTask DisposeAsync() => session.DisposeAsync();
}
