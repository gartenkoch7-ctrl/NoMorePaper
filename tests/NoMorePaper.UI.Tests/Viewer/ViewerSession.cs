using Microsoft.Playwright;
using Xunit;
using NoMorePaper.UI.Tests.Hosting;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>A browser session showing the viewer for the mocked document <see cref="Id"/>.</summary>
internal sealed class ViewerSession(BrowserSession session, Guid id, int[] textRequests) : IAsyncDisposable
{
    public IPage Page => session.Page;

    public Guid Id { get; } = id;

    /// <summary>How often the page asked for the recognized text (it may only do so once the tab is opened).</summary>
    public int TextRequests => Volatile.Read(ref textRequests[0]);

    /// <summary>True when the count is <paramref name="expected"/> now and stays there for half a second (a late request fails it).</summary>
    public async Task<bool> StaysAtAsync(int expected)
    {
        for (var i = 0; i < 10; i++)
        {
            if (TextRequests != expected)
            {
                return false;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return true;
    }

    public ValueTask DisposeAsync() => session.DisposeAsync();
}
