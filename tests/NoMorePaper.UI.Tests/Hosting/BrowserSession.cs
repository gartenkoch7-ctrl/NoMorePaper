using Microsoft.Playwright;

namespace NoMorePaper.UI.Tests.Hosting;

/// <summary>An isolated browser context (own storage) with one open page.</summary>
public sealed class BrowserSession(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page { get; } = page;

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
