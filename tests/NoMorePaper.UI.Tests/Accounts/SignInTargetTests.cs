using NoMorePaper.Web.Client.Accounts;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The page after signing in is only ever a path on this site (no open redirect).</summary>
public sealed class SignInTargetTests
{
    private const string Site = "https://archiv.example:8443/";

    [Theory]
    [InlineData("/")]
    [InlineData("/dokumente")]
    [InlineData("/ok?x=1")]
    [InlineData("/dokumente/42#seite-2")]
    // An encoded backslash stays encoded in the path: the browser does not read it as a slash.
    [InlineData("/%5Cevil")]
    public void PathsOnThisSite_AreKept(string target) =>
        Assert.Equal(target, SignInTarget.Safe(target, Site));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dokumente")]
    [InlineData("https://evil")]
    [InlineData("http://archiv.example:8443/")]
    [InlineData("//evil")]
    [InlineData("///evil")]
    [InlineData("/\\evil")]
    [InlineData("\\\\evil")]
    // Still encoded (e.g. double-encoded in the link): it does not start with a slash.
    [InlineData("%2F%09%2Fevil")]
    // Browsers drop tabs and line breaks, so these become "//evil".
    [InlineData("/\t/evil")]
    [InlineData("/\n/evil")]
    [InlineData("/\r/evil")]
    [InlineData(" //evil")]
    [InlineData("/ok evil")]
    [InlineData("/ok\u0007")]
    [InlineData("/ok\u00A0")]
    [InlineData("javascript:alert(1)")]
    public void AnythingElse_GoesToTheStartPage(string? target) =>
        Assert.Equal("/", SignInTarget.Safe(target, Site));

    [Fact]
    public void AnUnreadableBaseAddress_GoesToTheStartPage() =>
        Assert.Equal("/", SignInTarget.Safe("/dokumente", "not a uri"));
}
