using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>
/// An in-browser stand-in for the document, its pages and the edit calls: the page editor is tested without the
/// backend. The calls to <c>edit</c>, <c>split</c> and <c>append</c> are recorded in <see cref="Mocks"/>.
/// </summary>
internal sealed class FakePageEditor
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = "Stromrechnung";

    public bool CanEdit { get; init; } = true;

    public bool EditPending { get; init; }

    public bool IsPrivate { get; init; }

    public IReadOnlyList<PageInfoDto> Pages { get; init; } =
    [
        new(1, "Sehr geehrte Frau Muster", false, true),
        new(2, "", true, true),
        new(3, "Rechnungsnummer 4711", false, true),
    ];

    /// <summary>Answer of the three save calls (202 starts the edit; 409 means an edit runs).</summary>
    public int SaveStatus { get; set; } = 202;

    public IReadOnlyList<SearchHitDto> Hits { get; init; } = [];

    /// <summary>Split suggestion the review list carries for this document; <c>null</c> without one.</summary>
    public SplitSuggestionDto? Suggestion { get; init; }

    public AccountMocks Mocks { get; } = new();

    public string Path => $"/dokumente/{Id}/seiten";

    /// <summary>Opens the page and waits until it shows its state (list, error or a notice).</summary>
    public async Task<BrowserSession> OpenAsync(ServerFixture server, string culture = SupportedCultures.German, string? query = null)
    {
        var session = await server.OpenAsync(Path + query, culture, signedIn: true, prepare: async page =>
        {
            await new FakeAccounts().InstallAsync(page, signedIn: true);
            await InstallAsync(page);
        });
        try
        {
            await session.Page.Locator("main [data-ready]").WaitForAsync();
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>The card of page <paramref name="number"/> of <paramref name="total"/>, found by its heading.</summary>
    public static ILocator Card(IPage page, int number, int total, string culture = SupportedCultures.German)
        => page.GetByRole(AriaRole.Article, new() { Name = culture == SupportedCultures.German ? $"Seite {number} von {total}" : $"Page {number} of {total}" });

    /// <summary>The live region that announces the actions.</summary>
    public static ILocator Status(IPage page) => page.Locator("main .live-region").First;

    public async Task InstallAsync(IPage page)
    {
        var pdf = EditingPdfs.ThreePages();
        var detail = new DocumentDetailDto(
            Id, Title, DateTimeOffset.UtcNow, DocumentProcessingState.Done, null, null,
            new DocumentFileDto("brief.pdf", "application/pdf", pdf.Length, "0"), IsPrivate: IsPrivate, IsMine: true);
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{Id}$"), "GET", detail);
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{Id}/pages$"), "GET", new DocumentPagesDto(Id, Title, CanEdit, EditPending, Pages));
        await page.RouteAsync(new Regex($"/api/documents/{Id}/files/archive$"), route => route.FulfillAsync(
            new RouteFulfillOptions { Status = 200, ContentType = "application/pdf", BodyBytes = pdf }));
        await ApiMocks.SearchAsync(page, Hits);
        if (Suggestion is not null)
        {
            var item = new ReviewItemDto(Id, Title, DateTimeOffset.UtcNow, ClassificationSamples.Automatic(), null, Suggestion);
            await page.RouteAsync(new Regex("/api/review$"), route => route.Request.Method == "GET"
                ? route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = JsonSerializer.Serialize(new[] { item }, _json) })
                : route.FallbackAsync());
        }

        foreach (var call in new[] { "edit", "split", "append" })
        {
            await Mocks.RespondAsync(page, "POST", $"/api/documents/{Id}/{call}", SaveStatus);
        }
    }

    /// <summary>The JSON of the plan the page sent (the multipart body carries it as its first part).</summary>
    public string? PlanBody(string call) => Mocks.BodyOf("POST", $"/api/documents/{Id}/{call}");
}
