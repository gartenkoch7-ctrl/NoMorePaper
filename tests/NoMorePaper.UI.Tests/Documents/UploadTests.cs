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

    [Fact]
    public async Task FileInput_IsReachableByTab_WithVisibleFocus()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;

        FocusState focus = new();
        for (var i = 0; i < 15 && focus.Id != "upload-input"; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            focus = await FocusState.ReadAsync(page);
        }

        Assert.Equal("upload-input", focus.Id);
        Assert.True(focus.IndicatorWidth >= 3, $"Focus indicator {focus.IndicatorWidth}px");
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
        await Assertions.Expect(status).ToContainTextAsync("brief.pdf wurde hochgeladen und wartet auf die Verarbeitung.");
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

    // Needs the real API (track A): the document lands in the list as "Queued".
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
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync("wurde hochgeladen");

        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Dokumente", Exact = true }).ClickAsync();
        var row = page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = Path.GetFileNameWithoutExtension(name) });
        await Assertions.Expect(row).ToContainTextAsync("In Warteschlange");
    }
}
