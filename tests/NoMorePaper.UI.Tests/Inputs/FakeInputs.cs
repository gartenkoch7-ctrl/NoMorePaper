using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>
/// An in-browser stand-in for the inputs API: the state rows of "Eingänge", the account's preferences,
/// the household and the owner check of one document. It keeps what the page changed, so the page's reload
/// sees what the server would answer.
/// </summary>
internal sealed class FakeInputs
{
    public static readonly Guid Ben = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AccountMocks Mocks { get; } = new();

    public FakeAccounts Accounts { get; init; } = new();

    /// <summary>What <c>GET /api/inputs</c> answers.</summary>
    public List<InputStatusDto> Rows { get; } = [];

    public int InputsStatus { get; set; } = 200;

    public AccountPreferencesDto Preferences { get; set; } = new(false, []);

    public int PreferencesGetStatus { get; set; } = 200;

    /// <summary>The bodies of the saves the page sent, oldest first.</summary>
    public List<string> PreferenceBodies { get; } = [];

    /// <summary>Answer of saving preferences: 204, or 400/409 for a refused address.</summary>
    public int PreferencesPutStatus { get; set; } = 204;

    /// <summary>Answer of both owner-check calls: 204, or 409 for somebody who answered first.</summary>
    public int OwnerCheckStatus { get; set; } = 204;

    /// <summary>The document waits for the owner check until it is kept or handed over.</summary>
    public bool NeedsCheck { get; private set; } = true;

    public Guid DocumentId { get; } = Guid.NewGuid();

    public async Task<BrowserSession> OpenAsync(ServerFixture server, string path, string culture = SupportedCultures.German)
        => await server.OpenAsync(path, culture, signedIn: true, prepare: async page =>
        {
            await Accounts.InstallAsync(page, signedIn: true);
            await InstallAsync(page);
        });

    public async Task InstallAsync(IPage page)
    {
        await Json(page, "/api/inputs$", () => (InputsStatus, Rows));
        await Json(page, "/api/account/preferences$", () => (PreferencesGetStatus, Preferences));
        await page.RouteAsync(new Regex("/api/account/preferences$"), route => route.Request.Method != "PUT"
            ? route.FallbackAsync()
            : PutAsync(route));
        await Mocks.RespondAsync(page, "GET", "/api/household", 200, new HouseholdMemberDto[]
        {
            new(Accounts.Me.Id, "Anna Beispiel"), new(Ben, "Ben Beispiel"),
        });
    }

    /// <summary>The viewer of the document that needs the owner check, and its owner-check calls.</summary>
    public async Task InstallDocumentAsync(IPage page, string title = "Stromrechnung")
    {
        var file = new NoMorePaper.Contracts.Documents.DocumentFileDto("brief.png", "image/png", 3, "0");
        var detail = new NoMorePaper.Contracts.Documents.DocumentDetailDto(
            DocumentId, title, DateTimeOffset.UtcNow, NoMorePaper.Contracts.Documents.DocumentProcessingState.Queued, null, file, null, false, true);
        await Mocks.RespondAsync(page, "GET", $"/api/documents/{DocumentId}", 200, detail);
        await Json(page, $"/api/documents/{DocumentId}/fields$", () => (200, ClassificationSamples.Automatic() with { NeedsOwnerCheck = NeedsCheck }));
        await Mocks.RespondAsync(page, "POST", $"/api/documents/{DocumentId}/owner-check", OwnerCheckStatus, onCall: Answered);
        await Mocks.RespondAsync(page, "POST", $"/api/documents/{DocumentId}/hand-over", OwnerCheckStatus, onCall: Answered);
        await ClassificationMocks.ListsAsync(Mocks, page);
        await Mocks.RespondAsync(page, "PUT", $"/api/documents/{DocumentId}/editors", 204);
    }

    // A 409 means somebody answered first: the check is over either way, so the reload no longer shows it.
    private void Answered() => NeedsCheck = false;

    private static Task Json(IPage page, string path, Func<(int Status, object Body)> answer)
        => page.RouteAsync(new Regex(path), route =>
        {
            if (route.Request.Method != "GET")
            {
                return route.FallbackAsync();
            }

            var (status, body) = answer();
            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = status,
                ContentType = status >= 400 ? "application/problem+json" : "application/json",
                Body = JsonSerializer.Serialize(body, _json),
            });
        });

    private Task PutAsync(IRoute route)
    {
        var body = route.Request.PostData ?? string.Empty;
        PreferenceBodies.Add(body);
        if (PreferencesPutStatus == 204)
        {
            Preferences = JsonSerializer.Deserialize<AccountPreferencesDto>(body, _json)!;
            return route.FulfillAsync(new RouteFulfillOptions { Status = 204 });
        }

        var word = PreferencesPutStatus == 409 ? "taken" : "invalid";
        return route.FulfillAsync(new RouteFulfillOptions
        {
            Status = PreferencesPutStatus,
            ContentType = "application/problem+json",
            Body = JsonSerializer.Serialize(new { status = PreferencesPutStatus, errors = new { mailSenders = new[] { word } } }, _json),
        });
    }
}
