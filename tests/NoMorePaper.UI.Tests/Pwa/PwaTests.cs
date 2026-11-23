using System.Text.Json;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Pwa;

/// <summary>The app is installable: manifest, icons and service worker are served and active.</summary>
public sealed class PwaTests(ServerFixture server)
{
    [Fact]
    public async Task Manifest_IsServed_WithTheDesignBackgroundAndReachableIcons()
    {
        using var client = new HttpClient { BaseAddress = server.BaseAddress };

        using var manifest = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("manifest.webmanifest", UriKind.Relative), TestContext.Current.CancellationToken));
        var root = manifest.RootElement;

        Assert.Equal("#131313", root.GetProperty("theme_color").GetString());
        Assert.Equal("#131313", root.GetProperty("background_color").GetString());
        foreach (var icon in root.GetProperty("icons").EnumerateArray())
        {
            var src = icon.GetProperty("src").GetString()!;
            using var response = await client.GetAsync(new Uri(src, UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccessStatusCode, $"Icon {src}: {response.StatusCode}");
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task ServiceWorker_IsRegisteredAndActive()
    {
        await using var session = await server.OpenAsync("/");

        var active = await session.Page.EvaluateAsync<bool>(
            "async () => !!(await navigator.serviceWorker.ready).active");

        Assert.True(active);
    }
}
