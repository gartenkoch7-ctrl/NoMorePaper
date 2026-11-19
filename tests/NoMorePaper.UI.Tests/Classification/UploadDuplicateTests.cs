using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>Uploading the same file again: the server answers 409 with the existing document, the page says so and links to it.</summary>
public sealed class UploadDuplicateTests(ServerFixture server)
{
    private static Task UploadAsync(IPage page) => page.Locator("#upload-input").SetInputFilesAsync(new FilePayload
    {
        Name = "brief.pdf",
        MimeType = "application/pdf",
        Buffer = SampleDocuments.MinimalPdf(),
    });

    [Theory]
    [InlineData(SupportedCultures.German, "Diese Datei ist schon vorhanden: „brief.pdf“. Sie wurde nicht noch einmal gespeichert.", "Vorhandenes Dokument zu „brief.pdf“ ansehen")]
    [InlineData(SupportedCultures.English, "This file exists already: “brief.pdf”. It was not stored again.", "Show the existing document for “brief.pdf”")]
    public async Task Duplicate_IsAnnounced_ShownAsText_AndLinksToTheExistingDocument(string culture, string message, string link)
    {
        var existing = Guid.NewGuid();
        await using var session = await server.OpenAsync("/hochladen", culture);
        var page = session.Page;
        await ApiMocks.UploadAsync(page, new { title = "Conflict", status = 409, existingId = existing }, 409);

        await UploadAsync(page);

        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync(message);
        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync(message);
        var anchor = page.GetByRole(AriaRole.Link, new() { Name = link });
        await Assertions.Expect(anchor).ToHaveAttributeAsync("href", $"dokumente/{existing}");
    }

    [Fact]
    public async Task Duplicate_WithoutAnId_StillSaysSo_WithoutALink()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var page = session.Page;
        await ApiMocks.UploadAsync(page, new { title = "Conflict", status = 409 }, 409);

        await UploadAsync(page);

        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync("Diese Datei ist schon vorhanden");
        await Assertions.Expect(page.Locator(".upload-results li a")).ToHaveCountAsync(0);
    }
}
