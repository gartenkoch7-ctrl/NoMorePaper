using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>The account page's choices for new documents and mail senders, and the upload page's preset.</summary>
public sealed class PreferencesTests(ServerFixture server)
{
    private const string Add = "#prefs-sender-new";

    [Fact]
    public async Task Section_ShowsTheSavedChoices_AsLabelledControls()
    {
        var fake = new FakeInputs { Preferences = new AccountPreferencesDto(true, ["anna@example.org", "a.b@example.org"]) };
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Eingänge und neue Dokumente" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Neue Dokumente privat ablegen")).ToBeCheckedAsync();
        await Assertions.Expect(page.Locator(".sender-list li")).ToHaveTextAsync(
            [new Regex("anna@example.org"), new Regex("a.b@example.org")]);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: anna@example.org" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Toggle_SavesAtOnce_AndSaysWhatIsNowTrue()
    {
        var fake = new FakeInputs();
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;

        await page.GetByLabel("Neue Dokumente privat ablegen").CheckAsync();

        await Assertions.Expect(page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=status]"))
            .ToHaveTextAsync("Neue Dokumente werden ab jetzt privat abgelegt.");
        Assert.Contains("\"newDocumentsPrivate\":true", fake.PreferenceBodies.Single(), StringComparison.Ordinal);

        await page.GetByLabel("Neue Dokumente privat ablegen").UncheckAsync();

        await Assertions.Expect(page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=status]"))
            .ToHaveTextAsync("Neue Dokumente werden ab jetzt mit dem Haushalt geteilt.");
    }

    [Fact]
    public async Task AddingAndRemovingAnAddress_ByKeyboard_SavesAnnouncesAndKeepsFocusSensible()
    {
        var fake = new FakeInputs();
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;
        var status = page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=status]");

        await page.Locator(Add).FillAsync("  Anna@Example.ORG ");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(status).ToHaveTextAsync("Adresse anna@example.org hinzugefügt.");
        await Assertions.Expect(page.Locator(Add)).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.Locator(Add)).ToBeFocusedAsync();
        Assert.Contains("\"mailSenders\":[\"anna@example.org\"]", fake.PreferenceBodies.Single(), StringComparison.Ordinal);

        var remove = page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: anna@example.org" });
        await remove.FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(status).ToHaveTextAsync("Adresse anna@example.org entfernt.");
        await Assertions.Expect(remove).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(Add)).ToBeFocusedAsync();
        Assert.Contains("\"mailSenders\":[]", fake.PreferenceBodies[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(409, "Diese Absender-Adresse ist schon bei einem anderen Konto eingetragen.")]
    [InlineData(400, "Eine Absender-Adresse ist ungültig, oder es sind mehr als 20.")]
    public async Task ARefusedAddress_IsSaidAsText_KeepsTheInput_AndFocusesIt(int status, string text)
    {
        var fake = new FakeInputs { PreferencesPutStatus = status };
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;

        await page.Locator(Add).FillAsync("ben@example.org");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=alert]")).ToHaveTextAsync("Fehler: " + text);
        await Assertions.Expect(page.Locator(Add)).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(page.Locator(Add)).ToHaveValueAsync("ben@example.org");
        await Assertions.Expect(page.Locator(Add)).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByText("Noch keine Adresse eingetragen.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AnEmptyOrKnownAddress_IsRefusedWithoutAskingTheServer()
    {
        var fake = new FakeInputs { Preferences = new AccountPreferencesDto(false, ["anna@example.org"]) };
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;
        var error = page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=alert]");

        await page.Locator(Add).FillAsync(" ");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(error).ToContainTextAsync("Bitte fülle das Feld Absender-Adresse hinzufügen aus.");

        await page.Locator(Add).FillAsync("ANNA@example.org");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(error).ToHaveTextAsync("Fehler: Diese Adresse steht schon in deiner Liste.");
        Assert.Empty(fake.PreferenceBodies);
    }

    [Fact]
    public async Task ATwentyFirstAddress_IsRefusedWithTheMaximum_NotAsInvalid()
    {
        var full = Enumerable.Range(1, 20).Select(i => $"absender{i}@example.org").ToArray();
        var fake = new FakeInputs { Preferences = new AccountPreferencesDto(false, full) };
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;

        await page.Locator(Add).FillAsync("neu@example.org");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=alert]"))
            .ToHaveTextAsync("Fehler: Es sind höchstens 20 Absender-Adressen möglich. Entferne zuerst eine.");
        await Assertions.Expect(page.Locator(Add)).ToBeFocusedAsync();
        Assert.Empty(fake.PreferenceBodies);
    }

    [Fact]
    public async Task ARefusedToggle_IsSaidAsText_AndTheSwitchShowsWhatIsSaved()
    {
        var fake = new FakeInputs { PreferencesPutStatus = 500 };
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;

        await page.GetByLabel("Neue Dokumente privat ablegen").ClickAsync();

        await Assertions.Expect(page.Locator("section[aria-labelledby=inputs-prefs-heading] [role=alert]")).ToContainTextAsync("Fehler:");
        await Assertions.Expect(page.GetByLabel("Neue Dokumente privat ablegen")).Not.ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Neue Dokumente privat ablegen")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task LoadError_IsShownAsText_WithRetry()
    {
        var fake = new FakeInputs { PreferencesGetStatus = 500 };
        await using var session = await fake.OpenAsync(server, "/konto");
        var page = session.Page;

        await Assertions.Expect(page.GetByText("Die Einstellungen konnten nicht geladen werden")).ToBeVisibleAsync();
        fake.PreferencesGetStatus = 200;
        await page.Locator("#prefs-retry").ClickAsync();

        await Assertions.Expect(page.GetByLabel("Neue Dokumente privat ablegen")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UploadPage_PresetsThePrivateSwitch_FromThePreferences()
    {
        var fake = new FakeInputs { Preferences = new AccountPreferencesDto(true, []) };
        await using var session = await fake.OpenAsync(server, "/hochladen");

        await Assertions.Expect(session.Page.Locator("#upload-private")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task UploadPage_WhenThePreferencesCannotBeLoaded_TheAccountDecides_AndNoChoiceIsSent()
    {
        var fake = new FakeInputs { PreferencesGetStatus = 500 };
        await using var session = await fake.OpenAsync(server, "/hochladen");
        var page = session.Page;
        await NoMorePaper.UI.Tests.Documents.ApiMocks.UploadAsync(page, new { id = Guid.NewGuid(), isPrivate = true });
        var sent = page.WaitForRequestAsync(request => request.Method == "POST" && request.Url.EndsWith("/api/documents", StringComparison.Ordinal));

        await Assertions.Expect(page.Locator("#upload-private-hint")).ToContainTextAsync("Stand unbekannt: Deine Konto-Einstellung ließ sich nicht laden.");
        await Assertions.Expect(page.Locator("#upload-private")).ToHaveAttributeAsync("data-ready", "true");
        await Assertions.Expect(page.Locator("#upload-private")).ToHaveJSPropertyAsync("indeterminate", true);
        await page.Locator("#upload-input").SetInputFilesAsync(new FilePayload
        {
            Name = "brief.pdf",
            MimeType = "application/pdf",
            Buffer = NoMorePaper.UI.Tests.Documents.SampleDocuments.MinimalPdf(),
        });

        var body = (await sent).PostData ?? string.Empty;
        Assert.Contains("brief.pdf", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex("name=\"?private\"?"), body);
        await Assertions.Expect(page.Locator(".upload-results li")).ToContainTextAsync("brief.pdf wurde privat hochgeladen");
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task AccountPage_WithAddresses_HasNoAxeViolations(string culture)
    {
        var fake = new FakeInputs { Preferences = new AccountPreferencesDto(true, ["anna@example.org"]) };
        await using var session = await fake.OpenAsync(server, "/konto", culture);
        await Assertions.Expect(session.Page.Locator("#prefs-sender-new")).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }
}
