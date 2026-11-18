using Microsoft.Playwright;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>Fills the setup form the way a person does and submits it with the keyboard.</summary>
internal static class SetupFlow
{
    public const string Password = "Passwort-fuer-den-Test";

    public static async Task FillAndSubmitAsync(IPage page, string password = Password, string repeat = Password)
    {
        await page.Locator("#setup-display").FillAsync("Anna Beispiel");
        await page.Locator("#setup-user").FillAsync("anna");
        await page.Locator("#setup-password").FillAsync(password);
        await page.Locator("#setup-repeat").FillAsync(repeat);
        await page.Keyboard.PressAsync("Enter");
    }
}
