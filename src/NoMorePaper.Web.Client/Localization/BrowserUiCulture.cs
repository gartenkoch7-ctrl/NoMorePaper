using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Web.Client.Telemetry;

namespace NoMorePaper.Web.Client.Localization;

/// <summary>Sets the .NET culture and the <c>lang</c> attribute of the HTML document.</summary>
public sealed class BrowserUiCulture(IJSRuntime js, ILogger<BrowserUiCulture> logger) : IUiCulture
{
    public string Current => SupportedCultures.Resolve(CultureInfo.CurrentUICulture.Name);

    public async Task ApplyAsync(string culture)
    {
        var resolved = SupportedCultures.Resolve(culture);
        var info = CultureInfo.GetCultureInfo(resolved);
        CultureInfo.DefaultThreadCurrentCulture = info;
        CultureInfo.DefaultThreadCurrentUICulture = info;
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;

        await js.SetValueAsync("document.documentElement.lang", resolved);
        logger.CultureApplied(resolved);
    }
}
