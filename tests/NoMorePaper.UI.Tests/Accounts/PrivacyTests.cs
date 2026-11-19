using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>"Private": the upload switch, the viewer switch (owner only), and the mark in list and search.</summary>
public sealed class PrivacyTests(ServerFixture server)
{
    private static readonly Guid _id = Guid.NewGuid();

    private async Task<BrowserSession> OpenViewerAsync(
        AccountMocks mocks, bool isMine = true, bool isPrivate = false, int patchStatus = 204)
    {
        var file = new DocumentFileDto("brief.png", "image/png", 3, "0");
        var detail = new DocumentDetailDto(_id, "Testbrief", DateTimeOffset.UtcNow, DocumentProcessingState.Queued,
            null, file, null, isPrivate, isMine);
        return await server.OpenAsync($"/dokumente/{_id}", SupportedCultures.German, prepare: async page =>
        {
            await mocks.RespondAsync(page, "GET", $"/api/documents/{_id}", 200, detail);
            await mocks.RespondAsync(page, "GET", $"/api/documents/{_id}/fields", 200, ClassificationSamples.NoFields());
            await mocks.RespondAsync(page, "PATCH", $"/api/documents/{_id}", patchStatus);
        });
    }

    [Fact]
    public async Task UploadSwitch_IsALabelledCheckbox_AndSendsTheFieldWithTheFile()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;
        string? sent = null;
        page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.EndsWith("/api/documents", StringComparison.Ordinal))
            {
                sent = request.PostData;
            }
        };
        await ApiMocks.UploadAsync(page, new { id = Guid.NewGuid() });
        var box = page.GetByLabel("Privat", new() { Exact = true });

        await Assertions.Expect(box).Not.ToBeCheckedAsync();
        await Assertions.Expect(box).ToHaveAttributeAsync("aria-describedby", "upload-private-hint");
        await box.CheckAsync();
        await page.Locator("#upload-input").SetInputFilesAsync(new FilePayload
        {
            Name = "geheim.pdf",
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync("geheim.pdf wurde privat hochgeladen");
        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync("geheim.pdf wurde privat hochgeladen");

        Assert.NotNull(sent);
        Assert.Matches(new Regex("name=\"?private\"?\\s+true"), sent);
    }

    // The announcement and the result say which way each file went, in both languages.
    [Theory]
    [InlineData(SupportedCultures.German, true, "a.pdf wurde privat hochgeladen")]
    [InlineData(SupportedCultures.German, false, "a.pdf wurde geteilt hochgeladen")]
    [InlineData(SupportedCultures.English, true, "a.pdf was uploaded as private")]
    [InlineData(SupportedCultures.English, false, "a.pdf was uploaded as shared")]
    public async Task UploadResult_SaysPrivateOrShared(string culture, bool isPrivate, string expected)
    {
        await using var session = await server.OpenAsync("/hochladen", culture);
        var page = session.Page;
        await ApiMocks.UploadAsync(page, new { id = Guid.NewGuid() });

        await page.Locator("#upload-private").SetCheckedAsync(isPrivate);
        await page.Locator("#upload-input").SetInputFilesAsync(new FilePayload
        {
            Name = "a.pdf",
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });

        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync(expected);
        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync(expected);
    }

    [Fact]
    public async Task ViewerSwitch_ForTheOwner_SavesAndAnnounces()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenViewerAsync(mocks);
        var page = session.Page;
        var box = page.GetByLabel("Privat", new() { Exact = true });

        await box.CheckAsync();

        await Assertions.Expect(page.Locator("main .viewer-announcer")).ToHaveTextAsync("Das Dokument ist jetzt privat.");
        Assert.Equal("""{"isPrivate":true}""", mocks.BodyOf("PATCH", $"/api/documents/{_id}"));
        await box.UncheckAsync();
        await Assertions.Expect(page.Locator("main .viewer-announcer")).ToHaveTextAsync("Das Dokument ist nicht mehr privat.");
    }

    [Fact]
    public async Task ViewerSwitch_WhenTheServerRefuses_GoesBack_KeepsFocus_AndSaysSo()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenViewerAsync(mocks, patchStatus: 404);
        var page = session.Page;
        var box = page.GetByLabel("Privat", new() { Exact = true });

        await box.ClickAsync();

        await Assertions.Expect(page.Locator("main .viewer-announcer")).ToContainTextAsync("Fehler:");
        await Assertions.Expect(page.GetByLabel("Privat", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Privat", new() { Exact = true })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ViewerSwitch_IsNotOfferedForSomeoneElsesDocument()
    {
        await using var session = await OpenViewerAsync(new AccountMocks(), isMine: false);

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Link, new() { Name = "Original herunterladen" })).ToBeVisibleAsync();
        await Assertions.Expect(session.Page.GetByLabel("Privat", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task List_MarksPrivateDocuments_AsTextWithSymbol()
    {
        var now = DateTimeOffset.UtcNow;
        DocumentSummaryDto[] documents =
        [
            new(Guid.NewGuid(), "Steuerbescheid", now, DocumentProcessingState.Queued, IsPrivate: true),
            new(Guid.NewGuid(), "Stromrechnung", now, DocumentProcessingState.Queued),
        ];
        await using var session = await server.OpenAsync("/dokumente", SupportedCultures.German,
            prepare: page => ApiMocks.ListAsync(page, documents));
        var page = session.Page;

        var rows = page.Locator("tbody tr");
        await Assertions.Expect(rows.Nth(0).Locator(".private-badge")).ToHaveTextAsync("Privat");
        await Assertions.Expect(rows.Nth(0).Locator(".private-badge svg")).ToHaveCountAsync(1);
        await Assertions.Expect(rows.Nth(1).Locator(".private-badge")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Search_MarksPrivateHits()
    {
        var now = DateTimeOffset.UtcNow;
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, new[]
        {
            new SearchHitDto(Guid.NewGuid(), "Steuerbescheid", now, "… Betrag …", 0.9, IsPrivate: true),
            new SearchHitDto(Guid.NewGuid(), "Stromrechnung", now, "… Betrag …", 0.5),
        });

        await page.Locator("#header-search-input").FillAsync("Betrag");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".search-hit").Nth(0).Locator(".private-badge")).ToHaveTextAsync("Privat");
        await Assertions.Expect(page.Locator(".search-hit").Nth(1).Locator(".private-badge")).ToHaveCountAsync(0);
    }
}
