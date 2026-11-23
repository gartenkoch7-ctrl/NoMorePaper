using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>The read-only "Angaben": tags as chips, notes in words, a hidden but reachable heading, load errors.</summary>
public sealed class FieldsSummaryTests(ServerFixture server)
{
    private static FakeFields Fake(bool isMine = false) => new(Guid.NewGuid(), ClassificationSamples.Automatic(), isMine);

    [Fact]
    public async Task Tags_AreChips_AndTheValuesComeInPairs()
    {
        await using var session = await Fake().OpenViewerAsync(server);
        var panel = session.Page.GetByRole(AriaRole.Region, new() { Name = "Angaben" });

        await Assertions.Expect(panel.Locator(".fields-tags li")).ToHaveTextAsync(["Strom"]);
        await Assertions.Expect(panel.GetByRole(AriaRole.Term)).ToHaveTextAsync(["Absender", "Dokumentart", "Dokumentdatum", "Betrag", "Tags"]);
        await Assertions.Expect(panel.GetByRole(AriaRole.Definition)).ToHaveTextAsync(
            ["Stadtwerke München", "Rechnung", "01.11.2026", "84,20 EUR", "Strom"]);
    }

    [Fact]
    public async Task TheNoteComesBeforeTheList_AndItsSymbolIsDecoration()
    {
        await using var session = await Fake().OpenViewerAsync(server);
        var panel = session.Page.GetByRole(AriaRole.Region, new() { Name = "Angaben" });

        await Assertions.Expect(panel.Locator(".fields-recognition")).ToContainTextAsync("Automatisch erkannt");
        await Assertions.Expect(panel.Locator(".fields-recognition svg")).ToHaveAttributeAsync("aria-hidden", "true");
        var before = await panel.Locator(".fields-recognition").EvaluateAsync<bool>(
            "note => !!(note.compareDocumentPosition(document.querySelector('.fields-grid')) & Node.DOCUMENT_POSITION_FOLLOWING)");
        Assert.True(before);
    }

    [Fact]
    public async Task TheHeadingIsReachableByRole_ButNotSeen()
    {
        await using var session = await Fake().OpenViewerAsync(server);
        var heading = session.Page.GetByRole(AriaRole.Heading, new() { Name = "Angaben", Level = 2 });

        await Assertions.Expect(heading).ToHaveCountAsync(1);
        var box = await heading.BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(box.Width <= 1);
    }

    [Fact]
    public async Task WhenLoadingFails_AnAlertAppears_AndRetryLoadsAgain()
    {
        var fake = Fake();
        var fail = true;
        await using var session = await server.OpenAsync($"/dokumente/{fake.Id}", signedIn: true, prepare: async page =>
        {
            await fake.InstallAsync(page);
            await page.RouteAsync(new Regex($"/api/documents/{fake.Id}/fields$"), route => route.Request.Method == "GET" && fail
                ? route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = """{"title":"Kaputt"}""" })
                : route.FallbackAsync());
        });
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "konnten nicht geladen werden" })).ToBeVisibleAsync();
        fail = false;
        await page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-summary")).ToContainTextAsync("Stromrechnung der Stadtwerke München");
    }

    [Fact]
    public async Task TheOwner_SeesTheSwitchPrivate_EvenWhenTheFieldsDoNotLoad()
    {
        var fake = Fake(isMine: true);
        await using var session = await server.OpenAsync($"/dokumente/{fake.Id}", signedIn: true, prepare: async page =>
        {
            await fake.InstallAsync(page);
            await page.RouteAsync(new Regex($"/api/documents/{fake.Id}/fields$"), route => route.Request.Method == "GET"
                ? route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = """{"title":"Kaputt"}""" })
                : route.FallbackAsync());
        });

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "konnten nicht geladen werden" })).ToBeVisibleAsync();
        await Assertions.Expect(session.Page.GetByLabel("Privat", new() { Exact = true })).ToBeVisibleAsync();
    }
}
