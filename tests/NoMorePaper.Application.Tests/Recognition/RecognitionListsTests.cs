using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Recognition;
using Xunit;

namespace NoMorePaper.Application.Tests.Recognition;

public sealed class RecognitionListsTests
{
    private static readonly EntrySummary s_bank = new(Guid.CreateVersion7(), "Sparkasse");
    private static readonly EntrySummary s_power = new(Guid.CreateVersion7(), "Stadtwerke München GmbH");
    private static readonly EntrySummary s_doctor = new(Guid.CreateVersion7(), "Dr. Weber");
    private static readonly EntrySummary s_signs = new(Guid.CreateVersion7(), "---");

    [Fact]
    public void Mentioned_FindsWholeNames_EarliestFirst_DespiteCaseAccentsAndLegalForm()
    {
        var text = new NormalizedText("Ihre STADTWERKE MÜNCHEN informieren: Abbuchung über die Sparkasse.\nDr.Weberstraße 5");

        var mentioned = RecognitionLists.Mentioned(text, [s_bank, s_doctor, s_power, s_signs]);

        Assert.Equal([s_power, s_bank], mentioned);
    }

    [Fact]
    public void Names_KeepsTheOrder_WhileAllFit_AndPutsThePreferredFirst_WhenNot()
    {
        Assert.Equal(["Sparkasse", "Stadtwerke München GmbH"], RecognitionLists.Names([s_power, s_bank, s_doctor], [s_bank], 2));
        Assert.Equal(["Stadtwerke München GmbH", "Sparkasse", "Dr. Weber"], RecognitionLists.Names([s_power, s_bank, s_doctor], [s_bank], 3));
    }

    [Fact]
    public void NormalizedText_Mentions_LongWordsAlsoInsideLongerWords_AndShortNamesOnlyWhole()
    {
        var text = new NormalizedText("Zahnarztpraxis HOFFMANN-Schulz, Mitglied der AOK");

        Assert.True(text.Mentions("Hoffmann Zahnarztpraxis Dr", 4));
        Assert.False(text.Mentions("Praxis Weber", 4));
        Assert.True(text.Mentions("A.O.K.", 4));
        Assert.False(text.Mentions("DAK", 4));
        Assert.False(text.Mentions("Dr Mi", 4));
        Assert.Equal(-1, text.PositionOf("..."));
    }
}
