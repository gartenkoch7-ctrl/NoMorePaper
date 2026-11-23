using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Separation;
using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Application.Tests.Separation;

/// <summary>The page signs of a scanned stack: blank pages, page counters, salutations.</summary>
public sealed class SeparationHeuristicsTests
{
    private const string Letter = "Stadtwerke München GmbH\nHerrn Max Mustermann\nMünchen, 01.11.2026\nAbschlag Strom\nSehr geehrter Herr Mustermann,\nIhr Abschlag";

    private const string Continued = "der Betrag wird am 15.11.2026 von Ihrem Konto abgebucht. Bei Fragen erreichen Sie uns";

    [Fact]
    public void IsScannedStack_NeedsSeveralPages_WithoutTextLayer()
    {
        Assert.True(SeparationHeuristics.IsScannedStack([Page(1, Letter), Page(2, Continued)]));
        Assert.False(SeparationHeuristics.IsScannedStack([Page(1, Letter)]));
        Assert.False(SeparationHeuristics.IsScannedStack([Page(1, Letter), new PageInfo(2, Continued, false, HadTextLayer: true)]));
        Assert.False(SeparationHeuristics.IsScannedStack([]));
    }

    [Theory]
    [InlineData("Rechnung 4711\nSeite 1 von 3\nKundennummer", 1)]
    [InlineData("Seite 2 von 3", 2)]
    [InlineData("SEITE 2/3", 2)]
    [InlineData("Page 3 of 4", 3)]
    [InlineData("Blatt 1 von 2", 1)]
    [InlineData("S. 2 von 2", 2)]
    [InlineData("Kontoauszug\n  2 / 5  \nBuchungen", 2)]
    public void PageNumber_ReadsTheCounter(string head, int number) =>
        Assert.Equal(number, SeparationHeuristics.PageNumber(head));

    [Theory]
    [InlineData("Datum 1/3/2026")]
    [InlineData("Seite 4 von 3")]
    [InlineData("Seite 0 von 3")]
    [InlineData("davon 1/3 als Anzahlung")]
    [InlineData("Sehr geehrte Frau Muster,")]
    public void PageNumber_IgnoresWhatIsNoCounter(string head) =>
        Assert.Null(SeparationHeuristics.PageNumber(head));

    [Theory]
    [InlineData("Absender\n\nSehr geehrte Damen und Herren,", true)]
    [InlineData("  sehr geehrter Herr Muster,", true)]
    [InlineData("Guten Tag Frau Muster,", true)]
    [InlineData("Dear Mr Smith,", true)]
    [InlineData("wie wir Ihnen bereits schrieben, sehr geehrte Frau Muster", false)]
    [InlineData("Mit freundlichen Grüßen", false)]
    public void HasSalutation_OnlyAtTheStartOfALine(string head, bool expected) =>
        Assert.Equal(expected, SeparationHeuristics.HasSalutation(head));

    [Fact]
    public void Find_StartsADocument_AfterABlankPage_AndOnSalutationsAndFirstPages()
    {
        var candidates = SeparationHeuristics.Find(
        [
            Page(1, Letter), Page(2, Continued), Blank(3), Page(4, "Rechnung\nSeite 1 von 2"), Page(5, "Seite 2 von 2"),
            Page(6, "Finanzamt\nBescheid\nSehr geehrte Frau Muster,"),
        ]);

        Assert.Equal(
            [(1, SplitReason.Salutation), (4, SplitReason.Blank), (6, SplitReason.Salutation)],
            candidates.Starts.OrderBy(start => start.Key).Select(start => (start.Key, start.Value)));
        Assert.Equal([5], candidates.Continuations.Order());
        Assert.Equal([3], candidates.BlankPages);
        Assert.Equal(6, candidates.PageCount);
    }

    [Fact]
    public void Find_ReadsEmptyBackSides_OfADuplexScan_AsNoSeparator()
    {
        var candidates = SeparationHeuristics.Find([Page(1, Letter), Blank(2), Page(3, Continued), Blank(4)]);

        Assert.Equal([1], candidates.Starts.Keys);
        Assert.Equal([2, 4], candidates.BlankPages);
    }

    [Fact]
    public void Find_OrdersThePages_ByNumber()
    {
        var candidates = SeparationHeuristics.Find([Page(3, Letter), Page(1, Letter), Page(2, Continued)]);

        Assert.Equal([1, 3], candidates.Starts.Keys.Order());
    }

    private static PageInfo Page(int number, string head) => new(number, head, ProbablyBlank: false, HadTextLayer: false);

    private static PageInfo Blank(int number) => new(number, string.Empty, ProbablyBlank: true, HadTextLayer: false);
}
