using Xunit;
using NoMorePaper.Web.Client.Layout;

namespace NoMorePaper.Web.Client.Tests.Layout;

public sealed class NavSectionTests
{
    [Theory]
    [InlineData("", NavItem.Start)]
    [InlineData("?x=1", NavItem.Start)]
    [InlineData("hinzufuegen", NavItem.Add)]
    [InlineData("hochladen", NavItem.Add)]
    [InlineData("kamera", NavItem.Add)]
    [InlineData("dokumente", NavItem.Documents)]
    [InlineData("dokumente/3f2b8c1e-0000-0000-0000-000000000000", NavItem.Documents)]
    [InlineData("suche?q=strom", NavItem.Documents)]
    [InlineData("zu-pruefen", NavItem.Review)]
    [InlineData("einstellungen", NavItem.Settings)]
    [InlineData("konto", NavItem.Settings)]
    [InlineData("eingaenge", NavItem.Settings)]
    [InlineData("eingaenge/mail#postfach", NavItem.Settings)]
    [InlineData("listen", NavItem.Settings)]
    [InlineData("papierkorb", NavItem.Settings)]
    [InlineData("mitglieder", NavItem.Settings)]
    public void MapsPagesToTheirMainItem(string path, NavItem expected) =>
        Assert.Equal(expected, NavSection.For(path));

    [Theory]
    [InlineData("anmelden")]
    [InlineData("einrichten")]
    [InlineData("not-found")]
    [InlineData("dokumentenliste")]
    public void OtherPagesHaveNoMainItem(string path) =>
        Assert.Null(NavSection.For(path));
}
