using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>A browser session showing the viewer for the mocked document <see cref="Id"/>.</summary>
internal sealed class ViewerSession(BrowserSession session, Guid id) : IAsyncDisposable
{
    public IPage Page => session.Page;

    public Guid Id { get; } = id;

    public ValueTask DisposeAsync() => session.DisposeAsync();
}
