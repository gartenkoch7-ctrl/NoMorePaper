using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// Opens the viewer for a document that exists only in the browser: the detail and the file
/// routes are answered by Playwright, then the app navigates to <c>/dokumente/{id}</c>.
/// </summary>
internal static class ViewerPages
{
    public const string Title = "Testbrief";
    public const string Stage = ".viewer-stage";
    public const string Scroller = ".viewer-scroller";
    public const string SampleText = "Erste Zeile\nZweite Zeile";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Opens the viewer. <paramref name="prepare"/> runs on the start page first (e.g. to take
    /// WebGL away); with <paramref name="archive"/> false only the original exists.
    /// </summary>
    public static async Task<ViewerSession> OpenAsync(
        ServerFixture server,
        string culture,
        byte[] pdf,
        Func<IPage, Task>? prepare = null,
        bool archive = true,
        string originalType = "application/pdf",
        DocumentProcessingState state = DocumentProcessingState.Done,
        DocumentFieldsDto? fields = null,
        bool isMine = false,
        bool isPrivate = false,
        string? text = SampleText,
        int textStatus = 200)
    {
        var session = await server.OpenAsync("/", culture);
        try
        {
            var page = session.Page;
            var id = Guid.NewGuid();
            if (prepare is not null)
            {
                await prepare(page);
            }

            var original = new DocumentFileDto("brief.pdf", originalType, pdf.Length, "0");
            var archiveFile = archive ? new DocumentFileDto("brief.pdf", "application/pdf", pdf.Length, "0") : null;
            var detail = new DocumentDetailDto(id, Title, DateTimeOffset.UtcNow, state, null, original, archiveFile, IsMine: isMine, IsPrivate: isPrivate);
            await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{id}$"), "GET", detail);
            // The "Angaben" panel asks for the fields; nothing recognized, nothing editable here.
            await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{id}/fields$"), "GET", fields ?? ClassificationSamples.NoFields());
            var textRequests = new int[1];
            await page.RouteAsync(new Regex($"/api/documents/{id}/text$"), route =>
            {
                Interlocked.Increment(ref textRequests[0]);
                return route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = textStatus,
                    ContentType = textStatus >= 400 ? "application/problem+json" : "application/json",
                    Body = JsonSerializer.Serialize(new DocumentTextDto(text), _json),
                });
            });
            await page.RouteAsync(new Regex($"/api/documents/{id}/files/(original|archive)$"), route => route.FulfillAsync(
                new RouteFulfillOptions { Status = 200, ContentType = "application/pdf", BodyBytes = pdf }));
            // Client-side navigation: the routes above are in place before the page fetches.
            await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"dokumente/{id}");
            await page.Locator("h1", new PageLocatorOptions { HasText = Title }).WaitForAsync();
            return new ViewerSession(session, id, textRequests);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>Waits until the first page is rendered and its state was reported.</summary>
    public static Task WaitUntilShownAsync(IPage page) => page.Locator($"{Stage}[data-ready]").WaitForAsync();

    public static Task SelectTabAsync(IPage page, string name) =>
        page.GetByRole(AriaRole.Tab, new() { Name = name, Exact = true }).ClickAsync();

    public static ILocator Announcement(IPage page) => page.Locator("main .viewer-announcer");
}
