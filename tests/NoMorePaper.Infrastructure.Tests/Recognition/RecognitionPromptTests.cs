using NoMorePaper.Application.Recognition;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

public sealed class RecognitionPromptTests
{
    private static readonly RecognitionRequest s_request = new(
        "Stadtwerke München\r\nAbschlag 84,20 EUR",
        ["Stadtwerke München", "AOK Bayern"],
        ["Rechnung", "Bescheid"],
        ["Strom", "Gesundheit"],
        [new RecognitionExample("Stadtwerke München", "Rechnung", ["Strom"], "Strom Oktober 2026")]);

    [Fact]
    public void System_HasTheRules_AndTheOwnersLists_WithUnixLineEnds()
    {
        var system = RecognitionPrompt.System(s_request);

        Assert.StartsWith("Du ordnest Dokumente eines privaten Haushalts ein.", system, StringComparison.Ordinal);
        Assert.Contains(
            "\nAbsender: Stadtwerke München; AOK Bayern\nDokumentarten: Rechnung; Bescheid\nSchlagwörter: Strom; Gesundheit\n\nAntworte auf Deutsch",
            system,
            StringComparison.Ordinal);
        Assert.DoesNotContain('\r', system);
        Assert.EndsWith("Was nicht im Text steht, ist null.", system, StringComparison.Ordinal);
    }

    [Fact]
    public void User_HasTheExamplesAsJsonLines_ThenTheText_ThenTheReminder()
    {
        var user = RecognitionPrompt.User(s_request);

        Assert.StartsWith(
            "Früher bestätigte Dokumente, deren Absender im Text vorkommt (Vorbild für Dokumentart, Schlagwörter und Titel):\n"
            + "{\"correspondent\":\"Stadtwerke München\",\"documentType\":\"Rechnung\",\"tags\":[\"Strom\"],\"title\":\"Strom Oktober 2026\"}\n\n"
            + "Dokumenttext:\n<<<\nStadtwerke München\nAbschlag 84,20 EUR\n>>>\nGib jetzt das JSON aus.",
            user,
            StringComparison.Ordinal);
    }

    [Fact]
    public void User_StartsWithTheText_WhenThereAreNoExamples()
    {
        var user = RecognitionPrompt.User(s_request with { Examples = [] });

        Assert.StartsWith("Dokumenttext:\n<<<\n", user, StringComparison.Ordinal);
    }
}
