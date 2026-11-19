using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Recognition;

public sealed class RecognitionChecksTests
{
    private const string Text = """
        Stadtwerke München GmbH · Emmy-Noether-Str. 2 · 80992 München
        Abschlagsrechnung Strom, München, 01.11.2026
        Abschlag November: 84,20 EUR
        """;

    private static readonly DateOnly s_today = new(2026, 11, 19);

    private static readonly EntrySummary[] s_correspondents =
    [
        new(Guid.CreateVersion7(), "Stadtwerke München"),
        new(Guid.CreateVersion7(), "Zahnarztpraxis Dr. med. dent. Hoffmann"),
    ];

    private static readonly RecognitionResult s_answer = new(
        "Stadtwerke München", "Rechnung", new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), ["Strom"], "Strom November 2026", "Abschlag.");

    [Fact]
    public void Apply_KeepsValuesTheTextBacksUp()
    {
        var (result, rejected) = RecognitionChecks.Apply(s_answer, Text, s_correspondents, s_today);

        Assert.Equal(s_answer.Correspondent, result.Correspondent);
        Assert.Equal((s_answer.DocumentDate, s_answer.Amount), (result.DocumentDate, result.Amount));
        Assert.Equal(["Strom"], result.Tags);
        Assert.Empty(rejected);
    }

    [Fact]
    public void Apply_DropsAListedCorrespondent_TheTextDoesNotName_ButKeepsANewName()
    {
        var wrong = RecognitionChecks.Apply(
            s_answer with { Correspondent = "zahnarztpraxis dr med dent hoffmann" }, Text, s_correspondents, s_today);
        var fresh = RecognitionChecks.Apply(s_answer with { Correspondent = "Netzbetreiber Süd" }, Text, s_correspondents, s_today);

        Assert.Null(wrong.Result.Correspondent);
        Assert.Equal([RecognitionChecks.Correspondent], wrong.Rejected);
        var asTag = RecognitionChecks.Apply(
            s_answer with { Correspondent = "Zahnarztpraxis Dr. med. dent. Hoffmann", Tags = ["Zahnarztpraxis Dr. med. dent. Hoffmann", "Zahn"] },
            Text,
            s_correspondents,
            s_today);
        Assert.Equal(["Zahn"], asTag.Result.Tags);
        Assert.Equal("Netzbetreiber Süd", fresh.Result.Correspondent);
    }

    [Fact]
    public void Apply_FindsAListedCorrespondent_DespiteCaseAccentsAndGluedWords()
    {
        var text = "STADTWERKEMÜNCHEN Kundenservice 01.11.2026 84,20";

        var (result, rejected) = RecognitionChecks.Apply(s_answer, text, s_correspondents, s_today);

        Assert.Equal("Stadtwerke München", result.Correspondent);
        Assert.Empty(rejected);
    }

    [Theory]
    [InlineData(2023, 11, 22)]
    [InlineData(2026, 12, 24)]
    public void Apply_DropsADate_WhoseYearIsNotInTheText_OrThatLiesAhead(int year, int month, int day)
    {
        var (result, rejected) = RecognitionChecks.Apply(
            s_answer with { DocumentDate = new DateOnly(year, month, day) }, Text, s_correspondents, s_today);

        Assert.Null(result.DocumentDate);
        Assert.Equal([RecognitionChecks.DocumentDate], rejected);
    }

    [Theory]
    [InlineData("Kassenbon 19.11.26 12:04 Summe 84,20", true)]
    [InlineData("Bon 19/11/26", true)]
    [InlineData("Filiale 26, Kasse 3, Summe 84,20", false)]
    [InlineData("Datum 19.11.2026", true)]
    [InlineData("Kundennummer 120261", false)]
    public void Apply_KeepsADate_WhoseYearIsWrittenInFullOrInAShortDate(string text, bool kept)
    {
        var (result, _) = RecognitionChecks.Apply(s_answer with { DocumentDate = new DateOnly(2026, 11, 19) }, text, [], s_today);

        Assert.Equal(kept, result.DocumentDate is not null);
    }

    [Fact]
    public void Apply_KeepsADateOfTomorrow_ForTimeZonesAheadOfUtc()
    {
        var (result, _) = RecognitionChecks.Apply(s_answer with { DocumentDate = s_today.AddDays(1) }, Text, [], s_today);

        Assert.Equal(s_today.AddDays(1), result.DocumentDate);
    }

    [Fact]
    public void Apply_DropsAnAmountNotInTheText()
    {
        var (result, rejected) = RecognitionChecks.Apply(s_answer with { Amount = new Money(99.99m, "EUR") }, Text, s_correspondents, s_today);

        Assert.Null(result.Amount);
        Assert.Equal([RecognitionChecks.Amount], rejected);
    }

    [Fact]
    public void Apply_DropsTagsRepeatingTypeOrCorrespondent_AndEmptyOnes()
    {
        var (result, rejected) = RecognitionChecks.Apply(
            s_answer with { Tags = ["rechnung", "Stadtwerke-München", "Strom", "strom", "  "] }, Text, s_correspondents, s_today);

        Assert.Equal(["Strom"], result.Tags);
        Assert.Equal([RecognitionChecks.Tags], rejected);
    }

    [Fact]
    public void Apply_TrimsNames_CutsTitleAndSummary_AndDropsUnusableNames()
    {
        var answer = s_answer with
        {
            DocumentType = "  Rechnung ",
            Correspondent = "---",
            Title = new string('T', 100),
            Summary = "  " + new string('S', 600),
        };

        var (result, rejected) = RecognitionChecks.Apply(answer, Text, s_correspondents, s_today);

        Assert.Equal(("Rechnung", (string?)null), (result.DocumentType, result.Correspondent));
        Assert.Equal((RecognitionChecks.MaxTitleLength, RecognitionChecks.MaxSummaryLength), (result.Title!.Length, result.Summary!.Length));
        Assert.Empty(rejected);
    }

    [Fact]
    public void Apply_CutsTheTitle_NeverInsideAnEmoji()
    {
        var (result, _) = RecognitionChecks.Apply(s_answer with { Title = new string('T', 79) + "😀 Ende" }, Text, s_correspondents, s_today);

        Assert.Equal(new string('T', 79), result.Title);
    }

    [Fact]
    public void Apply_LeavesBlankTitleAndSummaryEmpty()
    {
        var (result, _) = RecognitionChecks.Apply(s_answer with { Title = "  ", Summary = null }, Text, s_correspondents, s_today);

        Assert.Equal((null, null), (result.Title, result.Summary));
    }
}
