using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Documents;

namespace NoMorePaper.UI.Tests.Start;

/// <summary>
/// Empty answers for the calls the start page makes. Tests that begin signed out and then land on "/" need
/// them: unmocked, the real server answers 401 and the app signs the person out again.
/// </summary>
internal static class StartPageMocks
{
    public static async Task InstallAsync(IPage page)
    {
        await ApiMocks.ListAsync(page, Array.Empty<object>());
        await ApiMocks.RespondAsync(page, new Regex("/api/inputs$"), "GET", Array.Empty<object>());
    }
}
