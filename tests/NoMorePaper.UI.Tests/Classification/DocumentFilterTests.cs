using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>The document list: classification columns and the filter bar, with the result count announced.</summary>
public sealed class DocumentFilterTests(ServerFixture server)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private static DocumentSummaryDto Invoice() => new(
        Guid.NewGuid(), "Stromrechnung", DateTimeOffset.UtcNow, DocumentProcessingState.Done,
        Correspondent: ClassificationSamples.StadtwerkeMuenchen, DocumentType: ClassificationSamples.Rechnung,
        DocumentDate: new DateOnly(2026, 11, 1), Amount: new MoneyDto(84.20m, "EUR"), Recognition: DocumentRecognitionState.Automatic);

    private static DocumentSummaryDto Contract() => new(
        Guid.NewGuid(), "Handyvertrag", DateTimeOffset.UtcNow.AddDays(-1), DocumentProcessingState.Done,
        Correspondent: ClassificationSamples.Telekom, DocumentType: ClassificationSamples.Vertrag, Recognition: DocumentRecognitionState.Confirmed);

    // Answers the list: everything without a filter, the contract with a correspondent filter, nothing for "empty".
    private static Task ListAsync(IPage page, ConcurrentQueue<string> urls) => page.RouteAsync(new Regex(@"/api/documents\?"), route =>
    {
        urls.Enqueue(route.Request.Url);
        var url = route.Request.Url;
        DocumentSummaryDto[] body = url.Contains("amountFrom=1000", StringComparison.Ordinal) ? []
            : url.Contains("correspondentId", StringComparison.Ordinal) ? [Contract()]
            : [Invoice(), Contract()];
        return route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = JsonSerializer.Serialize(body, _json) });
    });

    private async Task<BrowserSession> OpenAsync(ConcurrentQueue<string> urls, string culture = SupportedCultures.German)
    {
        var mocks = new AccountMocks();
        return await ApiMocks.OpenViaNavigationAsync(server, culture, culture == SupportedCultures.German ? "Dokumente" : "Documents", async page =>
        {
            await ListAsync(page, urls);
            await ClassificationMocks.ListsAsync(mocks, page);
        });
    }

    [Fact]
    public async Task List_ShowsSenderTypeDateAndAmount_AndMarksAutomaticAsText()
    {
        await using var session = await OpenAsync(new ConcurrentQueue<string>());
        var row = session.Page.Locator("tbody tr").Filter(new() { HasText = "Stromrechnung" });

        await Assertions.Expect(row).ToContainTextAsync("Stadtwerke München");
        await Assertions.Expect(row).ToContainTextAsync("Rechnung");
        await Assertions.Expect(row).ToContainTextAsync("01.11.2026");
        await Assertions.Expect(row).ToContainTextAsync("84,20 EUR");
        await Assertions.Expect(row).ToContainTextAsync("Automatisch erkannt");
        await Assertions.Expect(session.Page.Locator("tbody tr").Filter(new() { HasText = "Handyvertrag" })).Not.ToContainTextAsync("Automatisch erkannt");
    }

    [Fact]
    public async Task FilterBar_IsClosedAtFirst_AndOpensWithTheDisclosureButton()
    {
        await using var session = await OpenAsync(new ConcurrentQueue<string>());
        var page = session.Page;
        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true });

        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator("#filter-body")).ToBeHiddenAsync();
        await toggle.PressAsync("Enter");

        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.GetByLabel("Absender")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Absender").Locator("option")).ToHaveTextAsync(["Alle", "Stadtwerke München", "Telekom", "Vodafone"]);
    }

    [Fact]
    public async Task Apply_SendsTheFilterAsQuery_AnnouncesTheCount_AndShowsHowManyFiltersAreOn()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await OpenAsync(urls);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true }).ClickAsync();
        await page.GetByLabel("Absender").SelectOptionAsync(new SelectOptionValue { Label = "Telekom" });
        await page.GetByLabel("Dokumentdatum von").FillAsync("2026-11-01");
        await page.GetByLabel("Betrag von").FillAsync("50,5");
        await page.GetByLabel("Privat oder geteilt").SelectOptionAsync(new SelectOptionValue { Label = "Nur geteilte" });
        var add = page.Locator("#filter-tags-add");
        await add.FillAsync("Strom");
        await add.PressAsync("ArrowDown");
        await add.PressAsync("Enter");
        await page.GetByLabel("Dokument muss alle gewählten Tags haben").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Filtern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("1 Dokument gefunden.");
        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(1);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Filter (5 aktiv)" })).ToBeVisibleAsync();
        var query = new Uri(urls.Last()).Query;
        Assert.Contains($"correspondentId={ClassificationSamples.Telekom.Id}", query, StringComparison.Ordinal);
        Assert.Contains("from=2026-11-01", query, StringComparison.Ordinal);
        Assert.Contains("amountFrom=50.5", query, StringComparison.Ordinal);
        Assert.Contains("isPrivate=false", query, StringComparison.Ordinal);
        Assert.Contains($"tagIds={ClassificationSamples.Strom.Id}", query, StringComparison.Ordinal);
        Assert.Contains("allTags=true", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_WithoutResults_SaysSoInTheListAndInTheAnnouncement()
    {
        await using var session = await OpenAsync(new ConcurrentQueue<string>());
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true }).ClickAsync();
        await page.GetByLabel("Betrag von").FillAsync("1000");
        await page.GetByRole(AriaRole.Button, new() { Name = "Filtern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("Keine Dokumente gefunden.");
        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Keine Dokumente passen zu diesem Filter.");
    }

    [Fact]
    public async Task Reset_ClearsTheFilter_AndAnnouncesTheFullCount()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await OpenAsync(urls);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true }).ClickAsync();
        await page.GetByLabel("Absender").SelectOptionAsync(new SelectOptionValue { Label = "Telekom" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Filtern", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(1);
        await page.GetByRole(AriaRole.Button, new() { Name = "Zurücksetzen" }).ClickAsync();

        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("2 Dokumente gefunden.");
        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(2);
        Assert.DoesNotContain("correspondentId", urls.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_WithAnInvalidAmount_AnnouncesIt_FocusesTheField_AndAsksNothing()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await OpenAsync(urls);
        var page = session.Page;
        var before = urls.Count;

        await page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true }).ClickAsync();
        await page.GetByLabel("Betrag bis").FillAsync("viel");
        await page.GetByRole(AriaRole.Button, new() { Name = "Filtern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".filter-body .form-error")).ToContainTextAsync("Fehler: Der Betrag ist keine gültige Zahl.");
        await Assertions.Expect(page.GetByLabel("Betrag bis")).ToBeFocusedAsync();
        Assert.Equal(before, urls.Count);
    }

    [Fact]
    public async Task FilterBar_InEnglish_UsesEnglishTexts()
    {
        await using var session = await OpenAsync(new ConcurrentQueue<string>(), SupportedCultures.English);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByLabel("Document date from")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Apply filter" })).ToBeVisibleAsync();
    }
}
