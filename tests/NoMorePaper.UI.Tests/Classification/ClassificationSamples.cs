using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>Sample API data for the classification page tests.</summary>
internal static class ClassificationSamples
{
    public static readonly EntryDto StadtwerkeMuenchen = new(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "Stadtwerke München");
    public static readonly EntryDto Telekom = new(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "Telekom");
    public static readonly EntryDto Vodafone = new(Guid.Parse("00000000-0000-0000-0000-0000000000a3"), "Vodafone");
    public static readonly EntryDto Rechnung = new(Guid.Parse("00000000-0000-0000-0000-0000000000b1"), "Rechnung");
    public static readonly EntryDto Vertrag = new(Guid.Parse("00000000-0000-0000-0000-0000000000b2"), "Vertrag");
    public static readonly EntryDto Strom = new(Guid.Parse("00000000-0000-0000-0000-0000000000c1"), "Strom");
    public static readonly EntryDto Steuer = new(Guid.Parse("00000000-0000-0000-0000-0000000000c2"), "Steuer");

    public static EntryDto[] Correspondents() => [StadtwerkeMuenchen, Telekom, Vodafone];

    public static EntryDto[] Types() => [Rechnung, Vertrag];

    public static EntryDto[] Tags() => [Strom, Steuer];

    /// <summary>A document nothing was recognized for, which the signed-in account may not edit.</summary>
    public static DocumentFieldsDto NoFields() => new(null, null, null, null, null, [], DocumentRecognitionState.None, null, [], false);

    /// <summary>The demo invoice, recognized automatically and not checked yet.</summary>
    public static DocumentFieldsDto Automatic(bool canEdit = true, IReadOnlyList<Guid>? editors = null) => new(
        StadtwerkeMuenchen,
        Rechnung,
        new DateOnly(2026, 11, 1),
        new MoneyDto(84.20m, "EUR"),
        "Stromrechnung der Stadtwerke München für Oktober über 84,20 Euro.",
        [Strom],
        DocumentRecognitionState.Automatic,
        null,
        editors ?? [],
        canEdit);

    public static ReviewItemDto Review(string title, DocumentFieldsDto? fields = null, DuplicateCandidateDto? duplicate = null) => new(
        Guid.NewGuid(), title, new DateTimeOffset(2026, 11, 2, 9, 30, 0, TimeSpan.Zero), fields ?? Automatic(), duplicate);

    public static DuplicateCandidateDto Candidate(string title) => new(
        Guid.NewGuid(), title, new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero), new DateOnly(2026, 11, 1), StadtwerkeMuenchen);
}
