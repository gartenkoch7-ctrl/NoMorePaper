using System.Text.Json;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>"Herkunft unklar": keep ("Passt so") or hand over, in the viewer and in "Zu prüfen", by keyboard.</summary>
public sealed class OwnerCheckTests(ServerFixture server)
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private Task<BrowserSession> ViewerAsync(FakeInputs fake, string culture = SupportedCultures.German)
        => server.OpenAsync($"/dokumente/{fake.DocumentId}", culture, signedIn: true, async page =>
        {
            await fake.Accounts.InstallAsync(page, signedIn: true);
            await fake.InstallAsync(page);
            await fake.InstallDocumentAsync(page);
        });

    [Fact]
    public async Task Viewer_SaysTheOriginIsUnclear_AsText_WithTheTwoWaysOut()
    {
        var fake = new FakeInputs();
        await using var session = await ViewerAsync(fake);
        var page = session.Page;

        var section = page.GetByRole(AriaRole.Region, new() { Name = "Herkunft unklar" });
        await Assertions.Expect(section).ToContainTextAsync("Herkunft unklar – nur für dich sichtbar.");
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Passt so: Stromrechnung" })).ToBeVisibleAsync();
        await Assertions.Expect(section.GetByLabel("Weitergeben an")).ToBeVisibleAsync();
        // The owner himself is not offered: only the other accounts.
        await Assertions.Expect(section.GetByRole(AriaRole.Option)).ToHaveTextAsync(["Konto wählen", "Ben Beispiel"]);
    }

    [Fact]
    public async Task Keep_SendsTheAnswer_Announces_AndTheSectionGoesAway()
    {
        var fake = new FakeInputs();
        await using var session = await ViewerAsync(fake);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Passt so: Stromrechnung" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .fields-panel .live-region").First).ToHaveTextAsync("„Stromrechnung“ bleibt bei dir.");
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Herkunft unklar" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#fields-edit")).ToBeFocusedAsync();
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{fake.DocumentId}/owner-check"));
    }

    [Fact]
    public async Task HandOver_ByKeyboard_SendsTheChosenAccount_AndSaysWhereItWent()
    {
        var fake = new FakeInputs();
        await using var session = await ViewerAsync(fake);
        var page = session.Page;

        await page.GetByLabel("Weitergeben an").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Weitergeben: Stromrechnung" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        var told = "„Stromrechnung“ an Ben Beispiel weitergegeben.";
        await Assertions.Expect(page.Locator("#fields-handed")).ToContainTextAsync(told);
        await Assertions.Expect(page.Locator("#fields-handed")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zu den Dokumenten" })).ToHaveAttributeAsync("href", "dokumente");
        var sent = JsonSerializer.Deserialize<HandOver>(fake.Mocks.BodyOf("POST", $"/api/documents/{fake.DocumentId}/hand-over")!, s_json)!;
        Assert.Equal(FakeInputs.Ben, sent.AccountId);
    }

    [Fact]
    public async Task HandOver_WithoutAChoice_SaysSo_AndFocusesTheSelection()
    {
        var fake = new FakeInputs();
        await using var session = await ViewerAsync(fake);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Weitergeben: Stromrechnung" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Herkunft unklar" }).GetByRole(AriaRole.Alert)).ToHaveTextAsync("Fehler: Wähle zuerst ein Konto.");
        await Assertions.Expect(page.GetByLabel("Weitergeben an")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByLabel("Weitergeben an")).ToHaveAttributeAsync("aria-invalid", "true");
        Assert.False(fake.Mocks.WasCalled("POST", $"/api/documents/{fake.DocumentId}/hand-over"));
    }

    [Fact]
    public async Task AnAnswerSomebodyGaveFirst_IsSaidAsText()
    {
        var fake = new FakeInputs { OwnerCheckStatus = 409 };
        await using var session = await ViewerAsync(fake);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Passt so: Stromrechnung" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Herkunft unklar" }).GetByRole(AriaRole.Alert))
            .ToHaveTextAsync("Fehler: Das Dokument wurde inzwischen schon geprüft. Lade die Seite neu.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Passt so: Stromrechnung" })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ReviewList_OffersTheSame_AndAnswersWithWhatIsLeft()
    {
        var fake = new FakeInputs();
        var item = ClassificationSamples.Review("Stromrechnung November", ClassificationSamples.Automatic() with { NeedsOwnerCheck = true });
        var review = new FakeReview(item);
        await using var session = await server.OpenAsync("/zu-pruefen", SupportedCultures.German, signedIn: true, prepare: async page =>
        {
            await fake.Accounts.InstallAsync(page, signedIn: true);
            await review.InstallAsync(page);
            await fake.InstallAsync(page);
            await fake.Mocks.RespondAsync(page, "POST", $"/api/documents/{item.Id}/hand-over", 204);
        });
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Herkunft unklar" })).ToBeVisibleAsync();
        await page.GetByLabel("Weitergeben an").SelectOptionAsync(new SelectOptionValue { Label = "Ben Beispiel" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Weitergeben: Stromrechnung November" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First).ToContainTextAsync("„Stromrechnung November“ an Ben Beispiel weitergegeben.");
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{item.Id}/hand-over"));
    }

    [Fact]
    public async Task ReviewList_LoadsTheHouseholdOnce_ForAllItsSections()
    {
        var fake = new FakeInputs();
        var unclear = ClassificationSamples.Automatic() with { NeedsOwnerCheck = true };
        var items = Enumerable.Range(1, 3).Select(i => ClassificationSamples.Review($"Brief {i}", unclear)).ToArray();
        var review = new FakeReview(items);
        await using var session = await server.OpenAsync("/zu-pruefen", SupportedCultures.German, signedIn: true, prepare: async page =>
        {
            await fake.Accounts.InstallAsync(page, signedIn: true);
            await review.InstallAsync(page);
            await fake.InstallAsync(page);
        });
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Herkunft unklar" })).ToHaveCountAsync(3);
        await Assertions.Expect(page.GetByRole(AriaRole.Option, new() { Name = "Ben Beispiel" })).ToHaveCountAsync(3);

        Assert.Single(fake.Mocks.Requests, request => request.Method == "GET" && request.Path.EndsWith("/api/household", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task Viewer_WithTheSection_HasNoAxeViolations(string culture)
    {
        var fake = new FakeInputs();
        await using var session = await ViewerAsync(fake, culture);
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Region, new() { Name = culture == SupportedCultures.German ? "Herkunft unklar" : "Origin unclear" })).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    private sealed record HandOver(Guid AccountId);
}
