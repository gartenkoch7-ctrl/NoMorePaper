using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>axe-core finds no violations while a crop frame is open and after crops, in both languages; a phone does not scroll sideways.</summary>
public sealed class PageCropAccessibilityTests(ServerFixture server)
{
    public static TheoryData<string> Cultures() => new(SupportedCultures.All);

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task WithTheCropFrameOpen_OnATurnedPage_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakePageEditor { Pdf = EditingPdfs.SkewedSheets }.OpenAsync(server, culture);
        var page = session.Page;
        await page.Locator("#pe-rr-2").ClickAsync();

        await page.Locator("#pe-crop-2").ClickAsync();

        await Assertions.Expect(page.Locator("#pe-corner-0-2")).ToBeFocusedAsync(new() { Timeout = PageCropTests.CropTimeoutMs });
        await Assertions.Expect(page.Locator(".pe-crop-handle")).ToHaveCountAsync(4);
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task AfterAnAutomaticCrop_AndWithoutASheet_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakePageEditor { Pdf = EditingPdfs.SkewedSheets }.OpenAsync(server, culture);
        var page = session.Page;

        await page.Locator("#pe-auto-1").ClickAsync();
        await Assertions.Expect(page.Locator("#pe-uncrop-1")).ToBeVisibleAsync(new() { Timeout = PageCropTests.CropTimeoutMs });
        await page.Locator("#pe-auto-3").ClickAsync();

        await Assertions.Expect(page.Locator("#pe-crop-hand-3")).ToBeVisibleAsync(new() { Timeout = PageCropTests.CropTimeoutMs });
        await AxeCheck.NoViolationsAsync(page);
    }

    [Fact]
    public async Task OnAPhone_TheOpenFrameDoesNotScrollSideways()
    {
        await using var session = await new FakePageEditor { Pdf = EditingPdfs.SkewedSheets }.OpenAsync(server);
        var page = session.Page;
        await page.SetViewportSizeAsync(375, 812);

        await page.Locator("#pe-crop-1").ClickAsync();

        await Assertions.Expect(page.Locator("#pe-corner-0-1")).ToBeFocusedAsync(new() { Timeout = PageCropTests.CropTimeoutMs });
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
    }
}
