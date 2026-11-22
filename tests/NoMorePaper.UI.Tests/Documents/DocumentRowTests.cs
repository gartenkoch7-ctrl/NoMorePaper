using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>The rows of the document list show the fields of Stitch screen 02, as text and with decorative symbols.</summary>
public sealed class DocumentRowTests(ServerFixture server)
{
    private Task<BrowserSession> OpenAsync(Guid[] ids)
    {
        var documents = SampleDocuments.Rich();
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = documents[i].Id;
        }

        return ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await ApiMocks.ListAsync(page, documents);
            await ApiMocks.CountsAsync(page, SampleDocuments.Counts(2, (ClassificationSamples.Rechnung, 1), (ClassificationSamples.Vertrag, 1)));
        });
    }

    [Fact]
    public async Task Row_ShowsExcerptTypeTagsDateSourceAmountAndRecognition()
    {
        await using var session = await OpenAsync(new Guid[2]);
        var row = session.Page.Locator("tbody tr").Filter(new() { HasText = "Stromrechnung" });

        await Assertions.Expect(row).ToContainTextAsync("Stromrechnung der Stadtwerke für Oktober.");
        await Assertions.Expect(row.Locator(".doc-type")).ToHaveTextAsync("Rechnung");
        await Assertions.Expect(row.Locator(".doc-tags")).ToContainTextAsync("Strom");
        await Assertions.Expect(row.Locator(".doc-tags span[aria-hidden=true]")).ToHaveTextAsync("#");
        await Assertions.Expect(row.Locator(".doc-date")).ToHaveTextAsync("01.11.2026");
        await Assertions.Expect(row).ToContainTextAsync("über Mail");
        await Assertions.Expect(row.Locator(".doc-amount")).ToHaveTextAsync("84,20 EUR");
        await Assertions.Expect(row).ToContainTextAsync("Automatisch erkannt");
        await Assertions.Expect(row.Locator(".nmp-badge").Filter(new() { HasText = "Privat" })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Row_OfAConfirmedDocument_SaysConfirmed()
    {
        await using var session = await OpenAsync(new Guid[2]);

        var row = session.Page.Locator("tbody tr").Filter(new() { HasText = "Handyvertrag" });

        await Assertions.Expect(row).ToContainTextAsync("Bestätigt");
        await Assertions.Expect(row.Locator(".nmp-badge").Filter(new() { HasText = "Privat" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Table_IsAFocusableNamedRegion_AndTheTitleLinksToTheViewer()
    {
        var ids = new Guid[2];
        await using var session = await OpenAsync(ids);
        var page = session.Page;

        var region = page.GetByRole(AriaRole.Region, new() { Name = "Alle Dokumente, neueste zuerst" });

        await Assertions.Expect(region).ToHaveAttributeAsync("tabindex", "0");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Stromrechnung" }))
            .ToHaveAttributeAsync("href", $"dokumente/{ids[0]}");
    }

    [Fact]
    public async Task OnAPhone_OnlyTheTableScrolls_NotThePage()
    {
        await using var session = await OpenAsync(new Guid[2]);
        var page = session.Page;
        await page.SetViewportSizeAsync(390, 844);

        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
        var fits = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth");

        Assert.True(fits);
    }
}
