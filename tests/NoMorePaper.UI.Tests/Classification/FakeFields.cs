using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>
/// An in-browser stand-in for the classification API of one document: it answers the fields, the
/// lists and the household, records what the page sent, and applies saving and confirming so the
/// page's reload sees the result. Opens the viewer for it.
/// </summary>
internal sealed class FakeFields(Guid id, DocumentFieldsDto fields, bool isMine = false)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly FakeAccounts _accounts = new();

    public AccountMocks Mocks { get; } = new();

    public Guid Id { get; } = id;

    public DocumentFieldsDto Current { get; private set; } = fields;

    /// <summary>Answer of the save call (204 saves).</summary>
    public int SaveStatus { get; set; } = 204;

    /// <summary>The signed-in account the household list must leave out (the owner).</summary>
    public Guid MeId => _accounts.Me.Id;

    public async Task<BrowserSession> OpenViewerAsync(ServerFixture server, string culture = SupportedCultures.German)
    {
        return await server.OpenAsync($"/dokumente/{Id}", culture, signedIn: true, prepare: InstallAsync);
    }

    public async Task InstallAsync(IPage page)
    {
        await _accounts.InstallAsync(page, signedIn: true);
        var file = new DocumentFileDto("brief.png", "image/png", 3, "0");
        var detail = new DocumentDetailDto(Id, "Stromrechnung", DateTimeOffset.UtcNow, DocumentProcessingState.Queued, null, file, null, false, isMine);
        await Mocks.RespondAsync(page, "GET", $"/api/documents/{Id}", 200, detail);
        await page.RouteAsync(new Regex($"/api/documents/{Id}/fields$"), route => route.Request.Method == "GET"
            ? route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(Current, _json),
            })
            : route.FallbackAsync());
        await Mocks.RespondAsync(page, "PUT", $"/api/documents/{Id}/fields", SaveStatus, onCall: Saved);
        await Mocks.RespondAsync(page, "POST", $"/api/documents/{Id}/confirm", 204, onCall: Confirmed);
        await Mocks.RespondAsync(page, "PUT", $"/api/documents/{Id}/editors", 204);
        await Mocks.RespondAsync(page, "GET", "/api/review", 200, Array.Empty<ReviewItemDto>());
        await Mocks.RespondAsync(page, "GET", "/api/household", 200, new HouseholdMemberDto[]
        {
            new(MeId, "Anna Beispiel"), new(Guid.Parse("00000000-0000-0000-0000-0000000000d1"), "Ben Beispiel"),
            new(Guid.Parse("00000000-0000-0000-0000-0000000000d2"), "Clara Beispiel"),
        });
        await ClassificationMocks.ListsAsync(Mocks, page);
    }

    private void Confirmed() => Current = Current with { Recognition = DocumentRecognitionState.Confirmed };

    private void Saved()
    {
        if (SaveStatus != 204)
        {
            return;
        }

        var sent = JsonSerializer.Deserialize<EditFieldsRequestDto>(Mocks.BodyOf("PUT", $"/api/documents/{Id}/fields")!, _json)!;
        var all = ClassificationSamples.Correspondents().Concat(ClassificationSamples.Types()).Concat(ClassificationSamples.Tags()).ToArray();
        Current = Current with
        {
            Correspondent = all.FirstOrDefault(e => e.Id == sent.CorrespondentId),
            DocumentType = all.FirstOrDefault(e => e.Id == sent.DocumentTypeId),
            DocumentDate = sent.DocumentDate,
            Amount = sent.Amount,
            Summary = sent.Summary,
            Tags = [.. all.Where(e => sent.TagIds.Contains(e.Id))],
            Recognition = DocumentRecognitionState.Confirmed,
        };
    }
}
