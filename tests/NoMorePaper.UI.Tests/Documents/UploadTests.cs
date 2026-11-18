using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Keyboard;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>The upload page works with keyboard and screen reader: label, hint, announcements.</summary>
public sealed class UploadTests(ServerFixture server)
{
    private const string FileInput = "#upload-input";

    [Theory]
    [InlineData(SupportedCultures.German, "Datei auswählen")]
    [InlineData(SupportedCultures.English, "Choose a file")]
    public async Task FileInput_HasVisibleLabel_AndDescribingHint(string culture, string label)
    {
        await using var session = await server.OpenAsync("/hochladen", culture);
        var page = session.Page;

        await Assertions.Expect(page.GetByLabel(label)).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("label[for='upload-input']")).ToHaveTextAsync(label);
        // The accepted types and the size limit are read out with the control.
        await Assertions.Expect(page.Locator(FileInput)).ToHaveAttributeAsync("aria-describedby", "upload-hint");
        await Assertions.Expect(page.Locator("#upload-hint")).ToContainTextAsync("100 MB");
    }

    // Choosing a file uploads it at once, so the "Private" switch must come first when tabbing.
    [Fact]
    public async Task PrivateSwitch_ComesRightBeforeTheFileInput_BothWithVisibleFocus()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;

        var stops = new List<FocusState>();
        for (var i = 0; i < 15 && (stops.Count == 0 || stops[^1].Id != "upload-input"); i++)
        {
            await page.Keyboard.PressAsync("Tab");
            stops.Add(await FocusState.ReadAsync(page));
        }

        Assert.Equal("upload-input", stops[^1].Id);
        Assert.Equal("upload-private", stops[^2].Id);
        Assert.All(stops[^2..], focus => Assert.True(focus.IndicatorWidth >= 3, $"Focus indicator {focus.IndicatorWidth}px on {focus.Id}"));
    }

    [Fact]
    public async Task Upload_Accepted_AnnouncesResult_AndLinksToList()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;
        var id = Guid.NewGuid();
        await ApiMocks.UploadAsync(page, new { id });

        await page.Locator(FileInput).SetInputFilesAsync(new FilePayload
        {
            Name = "brief.pdf",
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });

        var status = page.Locator("main [role=status]");
        await Assertions.Expect(status).ToContainTextAsync("brief.pdf wurde geteilt hochgeladen und wartet auf die Verarbeitung.");
        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync("brief.pdf wurde geteilt hochgeladen");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "brief.pdf in der Dokumentenliste zeigen" }))
            .ToHaveAttributeAsync("href", $"dokumente#doc-{id:N}");
    }

    [Theory]
    [InlineData(415, "Dieser Dateityp ist nicht erlaubt")]
    [InlineData(413, "Die Datei ist größer als 100 MB.")]
    [InlineData(500, "Der Server meldet einen Fehler (Code 500).")]
    public async Task Upload_Rejected_ExplainsWhyInText(int status, string expected)
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;
        await ApiMocks.UploadAsync(page, new { title = "ignored" }, status);

        await page.Locator(FileInput).SetInputFilesAsync(new FilePayload
        {
            Name = "notiz.txt",
            MimeType = "text/plain",
            Buffer = [.. "text"u8],
        });

        // Both the live region and the result list carry the reason, as text with a symbol.
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync("notiz.txt konnte nicht hochgeladen werden.");
        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync(expected);
    }

    [Theory]
    [InlineData(SupportedCultures.German, 22, "2 Dateien wurden nicht hochgeladen, weil höchstens 20 auf einmal gehen.",
        "Nicht hochgeladen: seite-21.pdf, seite-22.pdf.")]
    [InlineData(SupportedCultures.English, 21, "1 file was not uploaded because at most 20 can be uploaded at once.",
        "Not uploaded: seite-21.pdf.")]
    // The announcement names ten files and counts the rest; the page lists them all.
    [InlineData(SupportedCultures.German, 33, "13 Dateien wurden nicht hochgeladen",
        "Nicht hochgeladen: seite-21.pdf, seite-22.pdf, seite-23.pdf, seite-24.pdf, seite-25.pdf, seite-26.pdf, "
        + "seite-27.pdf, seite-28.pdf, seite-29.pdf, seite-30.pdf und 3 weitere.")]
    public async Task MoreThan20Files_UploadsTheFirst20_AndNamesTheFilesLeftOut(
        string culture, int count, string expected, string expectedNames)
    {
        await using var session = await server.OpenAsync("/hochladen", culture);
        var page = session.Page;
        await ApiMocks.UploadAsync(page, new { id = Guid.NewGuid() });
        var files = Enumerable.Range(1, count).Select(i => new FilePayload
        {
            Name = $"seite-{i:D2}.pdf",
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });

        await page.Locator(FileInput).SetInputFilesAsync(files);

        // Shown as text with a symbol, and announced together with the last result.
        await Assertions.Expect(page.Locator(".upload-skipped")).ToContainTextAsync(expected);
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync(expected);
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync(expectedNames);
        var skipped = Enumerable.Range(21, count - 20).Select(i => $"seite-{i:D2}.pdf").ToArray();
        await Assertions.Expect(page.Locator(".upload-skipped-names li")).ToHaveTextAsync(skipped);
        await Assertions.Expect(page.Locator(".upload-results li")).ToHaveCountAsync(20);
        await Assertions.Expect(page.Locator(".upload-results")).ToContainTextAsync("seite-20.pdf");
        await Assertions.Expect(page.Locator(".upload-results")).Not.ToContainTextAsync("seite-21.pdf");
    }

    // Against the real API (signed in as the test administrator): the document lands in the list as "Queued".
    [Fact]
    public async Task Upload_AgainstRealApi_ShowsDocumentAsQueuedInList()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;
        var name = $"rechnung-{Guid.NewGuid():N}.pdf";

        await page.Locator(FileInput).SetInputFilesAsync(new FilePayload
        {
            Name = name,
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync("wurde geteilt hochgeladen");

        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Dokumente", Exact = true }).ClickAsync();
        var row = page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = Path.GetFileNameWithoutExtension(name) });
        await Assertions.Expect(row).ToContainTextAsync("In Warteschlange");
    }
}
