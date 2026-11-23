using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>Switching the removal confirmation from one member to another must move focus to the new question.</summary>
public sealed class MembersRemoveSwitchTests(ServerFixture server)
{
    private static readonly AccountDto _admin = new(Guid.NewGuid(), "anna", "Anna Beispiel", true);
    private static readonly AccountDto _ben = new(Guid.NewGuid(), "ben", "Ben Beispiel", false);
    private static readonly AccountDto _carla = new(Guid.NewGuid(), "carla", "Carla Beispiel", false);

    [Fact]
    public async Task Remove_ForASecondMemberWhileOpen_FocusesTheNewHeading()
    {
        var mocks = new AccountMocks();
        var fake = new FakeAccounts { IsAdmin = true };
        await using var session = await server.OpenAsync("/mitglieder", SupportedCultures.German, signedIn: true, async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await mocks.RespondAsync(page, "GET", "/api/members", 200, new[] { _admin, _ben, _carla });
        });
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: Ben Beispiel" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ben Beispiel wirklich entfernen?" })).ToBeFocusedAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: Carla Beispiel" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Carla Beispiel wirklich entfernen?" })).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ben Beispiel wirklich entfernen?" })).ToHaveCountAsync(0);
    }
}
