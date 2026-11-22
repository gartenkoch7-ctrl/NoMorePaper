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

/// <summary>axe-core finds no violations on the new pages and areas, in both languages and in their open states.</summary>
public sealed class ClassificationAccessibilityTests(ServerFixture server)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> Cultures() => new(SupportedCultures.All);

    private static ReviewItemDto[] ReviewItems() =>
    [
        ClassificationSamples.Review("Stromrechnung November"),
        ClassificationSamples.Review("Stromrechnung (Scan)", ClassificationSamples.Automatic(), ClassificationSamples.Candidate("Stromrechnung (Original)")),
    ];

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ReviewPage_WithItemsAndADuplicate_HasNoAxeViolations(string culture)
    {
        var fake = new FakeReview(ReviewItems());
        await using var session = await fake.OpenAsync(server, culture);
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Article)).ToHaveCountAsync(2);

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ReviewPage_WithTheEditFormAndTheDeleteQuestionOpen_HasNoAxeViolations(string culture)
    {
        var items = ReviewItems();
        var fake = new FakeReview(items);
        await using var session = await fake.OpenAsync(server, culture);
        var page = session.Page;
        var german = culture == SupportedCultures.German;

        await page.Locator($"#review-edit-{items[0].Id:N}").ClickAsync();
        await Assertions.Expect(page.Locator($"#review-{items[0].Id:N}-title")).ToBeFocusedAsync();
        await page.Locator($"#review-delete-{items[1].Id:N}").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = german ? "„Stromrechnung (Scan)“ wirklich löschen?" : "Really delete “Stromrechnung (Scan)”?" })).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ReviewPage_WhenEmpty_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakeReview().OpenAsync(server, culture);
        await session.Page.Locator("main [data-ready]").First.WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ListsPage_WithTheRenameAndMergeQuestionsOpen_HasNoAxeViolations(string culture)
    {
        var mocks = new AccountMocks();
        var accounts = new FakeAccounts();
        await using var session = await server.OpenAsync("/listen", culture, signedIn: true, prepare: async page =>
        {
            await accounts.InstallAsync(page, signedIn: true);
            await mocks.RespondAsync(page, "GET", "/api/review", 200, Array.Empty<ReviewItemDto>());
            await ClassificationMocks.ListsAsync(mocks, page);
        });
        var page = session.Page;
        await page.Locator("#list-correspondents-select-" + ClassificationSamples.Telekom.Id.ToString("N")).CheckAsync();
        await page.Locator("#list-correspondents-select-" + ClassificationSamples.Vodafone.Id.ToString("N")).CheckAsync();
        await page.Locator("#list-correspondents-merge").ClickAsync();
        await page.Locator("#list-tags-rename-" + ClassificationSamples.Strom.Id.ToString("N")).ClickAsync();
        await Assertions.Expect(page.Locator("#list-tags-rename-input")).ToBeFocusedAsync();
        // The fixed header covers whatever scrolled under it; axe would flag that covered content,
        // not the page itself. Where focus scrolled to depends on the page's height.
        await page.EvaluateAsync("() => window.scrollTo(0, 0)");

        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task DocumentList_WithTheFilterBarOpen_HasNoAxeViolations(string culture)
    {
        var mocks = new AccountMocks();
        var documents = new[]
        {
            new DocumentSummaryDto(Guid.NewGuid(), "Stromrechnung", DateTimeOffset.UtcNow, DocumentProcessingState.Done,
                Correspondent: ClassificationSamples.StadtwerkeMuenchen, DocumentType: ClassificationSamples.Rechnung,
                DocumentDate: new DateOnly(2026, 11, 1), Amount: new MoneyDto(84.20m, "EUR"), Recognition: DocumentRecognitionState.Automatic),
        };
        await using var session = await ApiMocks.OpenViaNavigationAsync(
            server, culture, culture == SupportedCultures.German ? "Dokumente" : "Documents", async page =>
            {
                await page.RouteAsync(new Regex(@"/api/documents\?"), route => route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(documents, _json),
                }));
                await ClassificationMocks.ListsAsync(mocks, page);
            });
        var page = session.Page;
        await page.Locator("#filter-toggle").ClickAsync();
        await Assertions.Expect(page.Locator("#filter-correspondent option")).ToHaveCountAsync(4);
        var add = page.Locator("#filter-tags-add");
        await add.FillAsync("S");
        await add.PressAsync("ArrowDown");
        await Assertions.Expect(page.Locator("#filter-tags-add-list")).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Viewer_WithTheFieldsPanelAndTheOpenEditor_HasNoAxeViolations(string culture)
    {
        var fake = new FakeFields(Guid.NewGuid(), ClassificationSamples.Automatic(editors: []), isMine: true);
        await using var session = await fake.OpenViewerAsync(server, culture);
        var page = session.Page;
        await Assertions.Expect(page.GetByRole(AriaRole.Checkbox, new() { Name = "Ben Beispiel" })).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(page);

        await page.Locator("#fields-edit").ClickAsync();
        var input = page.Locator("#fields-correspondent");
        await Assertions.Expect(input).ToBeVisibleAsync();
        await input.PressAsync("ArrowDown");
        await Assertions.Expect(page.Locator("#fields-correspondent-list")).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Upload_WithADuplicateNotice_HasNoAxeViolations(string culture)
    {
        await using var session = await server.OpenAsync("/hochladen", culture);
        var page = session.Page;
        await ApiMocks.UploadAsync(page, new { existingId = Guid.NewGuid() }, 409);
        await page.Locator("#upload-input").SetInputFilesAsync(new FilePayload
        {
            Name = "brief.pdf",
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });
        await Assertions.Expect(page.Locator(".upload-results li a")).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(page);
    }
}
