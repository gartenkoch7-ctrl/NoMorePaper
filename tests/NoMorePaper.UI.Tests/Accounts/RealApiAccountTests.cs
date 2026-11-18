using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>
/// Account flows against the real server and its database; no browser mocks are involved.
/// </summary>
public sealed class RealApiAccountTests(ServerFixture server)
{
    [Fact]
    public async Task SignIn_WithTheRealAccount_ThenSignOut()
    {
        await using var session = await server.OpenAsync("/anmelden", SupportedCultures.German, signedIn: false);
        var page = session.Page;

        await page.Locator("#signin-user").FillAsync(ServerFixture.AdminUserName);
        await page.Locator("#signin-password").FillAsync(ServerFixture.AdminPassword);
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".account-name")).ToContainTextAsync("Test Admin");
        await page.GetByRole(AriaRole.Button, new() { Name = "Abmelden" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/anmelden$"));
        await page.GotoAsync("/dokumente");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/anmelden\?ziel=%2Fdokumente$"));
    }

    [Fact]
    public async Task WrongPassword_IsAnnounced_WithoutSigningIn()
    {
        await using var session = await server.OpenAsync("/anmelden", SupportedCultures.German, signedIn: false);
        var page = session.Page;

        await page.Locator("#signin-user").FillAsync(ServerFixture.AdminUserName);
        await page.Locator("#signin-password").FillAsync("das-ist-falsch");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync("Benutzername oder Passwort stimmen nicht");
        await Assertions.Expect(page.Locator(".account-name")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task WithoutASession_EveryPageGoesToSignIn()
    {
        await using var session = await server.OpenAsync("/hochladen", SupportedCultures.German, signedIn: false);

        await Assertions.Expect(session.Page).ToHaveURLAsync(new Regex(@"/anmelden\?ziel=%2Fhochladen$"));
        await Assertions.Expect(session.Page.Locator("h1")).ToHaveTextAsync("Anmelden");
    }

    // The demo of the milestone: invite a member, upload one private and one shared document;
    // the member sees only the shared one.
    [Fact]
    public async Task InvitedMember_NeverSeesThePrivateDocumentOfTheAdministrator()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var admin = await server.OpenAsync("/hochladen", SupportedCultures.German);
        var upload = admin.Page;
        await UploadAsync(upload, $"privat-{suffix}.pdf", isPrivate: true);
        await UploadAsync(upload, $"offen-{suffix}.pdf", isPrivate: false);

        await upload.GetByRole(AriaRole.Link, new() { Name = "Mitglieder", Exact = true }).ClickAsync();
        await upload.GetByRole(AriaRole.Button, new() { Name = "Neues Mitglied einladen" }).ClickAsync();
        var link = await upload.Locator("#invite-link").InputValueAsync();

        await using var member = await server.OpenAsync(new Uri(link).PathAndQuery, SupportedCultures.German, signedIn: false);
        var page = member.Page;
        await page.Locator("#invite-display").FillAsync("Mitglied");
        await page.Locator("#invite-user").FillAsync($"mitglied{suffix}");
        await page.Locator("#invite-password").FillAsync(SetupFlow.Password);
        await page.Locator("#invite-repeat").FillAsync(SetupFlow.Password);
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator(".account-name")).ToContainTextAsync("Mitglied");

        await page.GetByRole(AriaRole.Link, new() { Name = "Dokumente", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("tbody tr").Filter(new() { HasText = $"offen-{suffix}" })).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("tbody tr").Filter(new() { HasText = $"privat-{suffix}" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mitglieder", Exact = true })).ToHaveCountAsync(0);
    }

    private static async Task UploadAsync(IPage page, string name, bool isPrivate)
    {
        var box = page.GetByLabel("Privat", new() { Exact = true });
        await box.SetCheckedAsync(isPrivate);
        await page.Locator("#upload-input").SetInputFilesAsync(new FilePayload
        {
            Name = name,
            MimeType = "application/pdf",
            Buffer = SampleDocuments.MinimalPdf(),
        });
        await Assertions.Expect(page.Locator("main [role=status]")).ToContainTextAsync($"{name} wurde hochgeladen");
    }
}
