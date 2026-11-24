using System.Collections.Concurrent;
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

    /// <summary>Version of the document (the detail carries it); the page puts it into the preview URL.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>How many of the first loads of the pages fail with a server error (the retry button follows).</summary>
    public int FailedPageLoads { get; init; }

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

    /// <summary>The archive PDF the previews and the crop are drawn from; three pages.</summary>
    public Func<byte[]> Pdf { get; init; } = EditingPdfs.ThreePages;

    public AccountMocks Mocks { get; } = new();

    /// <summary>URLs the previews were requested from (the archive PDF), in request order.</summary>
    public ConcurrentQueue<string> ArchiveRequests { get; } = new();

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
        var pdf = Pdf();
        var detail = new DocumentDetailDto(
            Id, Title, DateTimeOffset.UtcNow, DocumentProcessingState.Done, null, null,
            new DocumentFileDto("brief.pdf", "application/pdf", pdf.Length, "0"), IsPrivate: IsPrivate, IsMine: true, EditedAt: EditedAt);
        // The detail is read when the page loads, so a test can change EditedAt before it comes back.
        await page.RouteAsync(new Regex($"/api/documents/{Id}$"), route => route.Request.Method == "GET"
            ? route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(detail with { EditedAt = EditedAt }, _json),
            })
            : route.ContinueAsync());
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{Id}/pages$"), "GET", new DocumentPagesDto(Id, Title, CanEdit, EditPending, Pages));
        var loads = 0;
        await page.RouteAsync(new Regex($"/api/documents/{Id}/pages$"), route => route.Request.Method == "GET" && Interlocked.Increment(ref loads) <= FailedPageLoads
            ? route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = "{\"title\":\"Fehler\"}" })
            : route.FallbackAsync());
        await page.RouteAsync(new Regex($"/api/documents/{Id}/files/archive(\\?.*)?$"), route =>
        {
            ArchiveRequests.Enqueue(route.Request.Url);
            return route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/pdf", BodyBytes = pdf });
        });
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

        // Registered last, so it sees the calls first: the raw body (JPEG parts are binary), then the answer above.
        await page.RouteAsync(new Regex($"/api/documents/{Id}/(edit|split)$"), route =>
        {
            var request = route.Request;
            if (request.Method == "POST")
            {
                Sent[new Uri(request.Url).Segments[^1]] = (request.Headers.GetValueOrDefault("content-type") ?? "", request.PostDataBuffer ?? []);
            }

            return route.FallbackAsync();
        });
    }

    /// <summary>The JSON of the plan the page sent (the multipart body carries it as its first part).</summary>
    public string? PlanBody(string call) => Mocks.BodyOf("POST", $"/api/documents/{Id}/{call}");

    /// <summary>The last body of <c>edit</c> or <c>split</c> as sent, by call.</summary>
    public ConcurrentDictionary<string, (string ContentType, byte[] Body)> Sent { get; } = new();
}
