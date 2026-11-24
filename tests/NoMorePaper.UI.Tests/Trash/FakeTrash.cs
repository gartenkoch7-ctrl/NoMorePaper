using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Trash;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Trash;

/// <summary>
/// An in-browser stand-in for the trash: it keeps the list, answers the actions the page calls and applies
/// them (restoring and deleting remove the entry, a swap adds the file that gave way), so the page's reload
/// after each action sees what the server would answer.
/// </summary>
internal sealed class FakeTrash(params TrashItemDto[] items)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly List<TrashItemDto> _items = [.. items];

    /// <summary>The calls the page made, as "METHOD path" (ids as written in the URL).</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Answer of the restore call (204 restores, 409 for an entry that changed meanwhile).</summary>
    public int RestoreStatus { get; set; } = 204;

    public int PurgeStatus { get; set; } = 204;

    public int EmptyStatus { get; set; } = 204;

    /// <summary>Answer of the list call; anything but 200 shows the load error.</summary>
    public int ListStatus { get; set; } = 200;

    public IReadOnlyList<TrashItemDto> Items => _items;

    public static TrashItemDto Entry(
        string title, TrashItemKindDto kind = TrashItemKindDto.Document, bool swaps = false, bool documentInTrash = false) =>
        new(Guid.NewGuid(), kind, title, new DateTimeOffset(2026, 2, 28, 11, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 30, 11, 15, 0, TimeSpan.Zero), swaps, documentInTrash);

    public async Task<BrowserSession> OpenAsync(ServerFixture server, string culture = SupportedCultures.German, int? width = null)
    {
        var accounts = new FakeAccounts();
        return await server.OpenAsync("/papierkorb", culture, signedIn: true, prepare: async page =>
        {
            if (width is { } w)
            {
                await page.SetViewportSizeAsync(w, 900);
            }

            await accounts.InstallAsync(page, signedIn: true);
            await InstallAsync(page);
        });
    }

    public async Task InstallAsync(IPage page)
    {
        await page.RouteAsync(new Regex("/api/trash$"), route => route.Request.Method switch
        {
            "GET" => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = ListStatus,
                ContentType = ListStatus >= 400 ? "application/problem+json" : "application/json",
                Body = JsonSerializer.Serialize(ListStatus >= 400 ? new { } : (object)_items, _json),
            }),
            "DELETE" => RespondAsync(route, EmptyStatus, _items.Clear),
            _ => route.FallbackAsync(),
        });
        await page.RouteAsync(new Regex("/api/trash/[0-9a-f-]{36}(/restore)?$"), route =>
        {
            var request = route.Request;
            var restore = request.Url.EndsWith("/restore", StringComparison.Ordinal);
            var id = Guid.Parse(Regex.Match(request.Url, "[0-9a-f-]{36}").Value);
            return (request.Method, restore) switch
            {
                ("POST", true) => RespondAsync(route, RestoreStatus, () => Restore(id)),
                ("DELETE", false) => RespondAsync(route, PurgeStatus, () => _items.RemoveAll(item => item.Id == id)),
                _ => route.FallbackAsync(),
            };
        });
    }

    private Task RespondAsync(IRoute route, int status, Action apply)
    {
        var request = route.Request;
        Calls.Add($"{request.Method} {new Uri(request.Url).AbsolutePath}");
        if (status == 204)
        {
            apply();
        }

        return route.FulfillAsync(new RouteFulfillOptions
        {
            Status = status,
            ContentType = status >= 400 ? "application/problem+json" : "application/json",
            Body = status == 204 ? null : "{}",
        });
    }

    // A swap puts the file that kept the place into the trash, as the server does.
    private void Restore(Guid id)
    {
        var item = _items.Single(other => other.Id == id);
        _items.Remove(item);
        if (item.RestoreSwaps)
        {
            _items.Add(Entry(item.Title + " (ältere Fassung)", TrashItemKindDto.Edited));
        }
    }
}
