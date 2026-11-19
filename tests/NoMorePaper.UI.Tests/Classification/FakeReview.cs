using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>
/// An in-browser stand-in for "Zu prüfen": it keeps the list, answers the actions the page calls and
/// applies them (confirming removes an item that has no duplicate, deleting removes it), so the
/// page's reload after each action sees what the server would answer.
/// </summary>
internal sealed class FakeReview(params ReviewItemDto[] items)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly List<ReviewItemDto> _items = [.. items];

    public AccountMocks Mocks { get; } = new();

    /// <summary>
    /// Answer of confirming and saving: 204, or 409 for somebody who confirmed the item meanwhile (it is
    /// confirmed either way, so the reload no longer lists it).
    /// </summary>
    public int ConfirmStatus { get; set; } = 204;

    /// <summary>Answer of the delete call (204 deletes).</summary>
    public int DeleteStatus { get; set; } = 204;

    public IReadOnlyList<ReviewItemDto> Items => _items;

    public async Task<BrowserSession> OpenAsync(ServerFixture server, string culture = SupportedCultures.German)
    {
        var accounts = new FakeAccounts();
        return await server.OpenAsync("/zu-pruefen", culture, signedIn: true, prepare: async page =>
        {
            await accounts.InstallAsync(page, signedIn: true);
            await InstallAsync(page);
        });
    }

    public async Task InstallAsync(IPage page)
    {
        await page.RouteAsync(new Regex("/api/review$"), route => route.Request.Method == "GET"
            ? route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(_items, _json),
            })
            : route.FallbackAsync());
        await page.RouteAsync(new Regex("/api/review/count$"), route => route.Request.Method == "GET"
            ? route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = _items.Count.ToString(CultureInfo.InvariantCulture) })
            : route.FallbackAsync());
        foreach (var item in _items.ToArray())
        {
            var id = item.Id;
            await Mocks.RespondAsync(page, "POST", $"/api/documents/{id}/confirm", ConfirmStatus, onCall: () => Replace(id, confirmed: true));
            await Mocks.RespondAsync(page, "POST", $"/api/documents/{id}/duplicate/keep", 204, onCall: () => Replace(id, keepDuplicate: true));
            await Mocks.RespondAsync(page, "DELETE", $"/api/documents/{id}", DeleteStatus, onCall: () => Remove(id));
            await Mocks.RespondAsync(page, "PUT", $"/api/documents/{id}/fields", ConfirmStatus, onCall: () => Replace(id, confirmed: true));
        }

        await ClassificationMocks.ListsAsync(Mocks, page);
    }

    private void Remove(Guid id)
    {
        if (DeleteStatus == 204)
        {
            _items.RemoveAll(item => item.Id == id);
        }
    }

    // An item leaves the list when nothing is left to review: not automatic any more and no duplicate hint.
    private void Replace(Guid id, bool confirmed = false, bool keepDuplicate = false)
    {
        var index = _items.FindIndex(item => item.Id == id);
        var item = _items[index];
        var fields = confirmed ? item.Fields with { Recognition = DocumentRecognitionState.Confirmed } : item.Fields;
        var updated = item with { Fields = fields, PossibleDuplicate = keepDuplicate ? null : item.PossibleDuplicate };
        if (updated.Fields.Recognition == DocumentRecognitionState.Automatic || updated.PossibleDuplicate is not null)
        {
            _items[index] = updated;
        }
        else
        {
            _items.RemoveAt(index);
        }
    }
}
