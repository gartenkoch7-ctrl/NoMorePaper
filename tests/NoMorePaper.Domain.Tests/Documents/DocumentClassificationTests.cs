using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class DocumentClassificationTests
{
    private static readonly DateTimeOffset s_receivedAt = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly Guid s_other = Guid.CreateVersion7();
    private static readonly Guid s_strom = Guid.CreateVersion7();
    private static readonly Guid s_gas = Guid.CreateVersion7();

    [Fact]
    public void ANewDocument_HasNoFields()
    {
        var document = Receive();

        Assert.Equal(RecognitionState.None, document.Recognition);
        Assert.Equal((null, null, null, null, null), (document.CorrespondentId, document.DocumentTypeId, document.DocumentDate, document.Amount, document.Summary));
        Assert.Empty(document.Tags);
        Assert.Null(document.PossibleDuplicateOf);
        Assert.Empty(document.Editors);
    }

    [Fact]
    public void Classify_TakesTheFields_AsAutomatic()
    {
        var document = Receive();
        var correspondent = Guid.CreateVersion7();
        var type = Guid.CreateVersion7();

        document.Classify(new DocumentFields("Stromrechnung November", correspondent, type, new DateOnly(2026, 11, 1), new Money(84.20m, "EUR"), "Abschlag.", [s_strom]));

        Assert.Equal(RecognitionState.Automatic, document.Recognition);
        Assert.Equal(("Stromrechnung November", correspondent, type), (document.Title, document.CorrespondentId, document.DocumentTypeId));
        Assert.Equal((new DateOnly(2026, 11, 1), new Money(84.20m, "EUR"), "Abschlag."), (document.DocumentDate, document.Amount, document.Summary));
        Assert.Equal([s_strom], document.Tags.Select(tag => tag.TagId));
    }

    [Fact]
    public void Classify_WithoutTitle_KeepsTheTitle()
    {
        var document = Receive();

        document.Classify(Fields(null));

        Assert.Equal("scan.pdf", document.Title);
    }

    [Fact]
    public void Classify_AgainBeforeConfirmation_ReplacesTheFields()
    {
        var document = Receive();
        document.Classify(Fields("Erst", s_strom));

        document.Classify(new DocumentFields("Zweit", null, null, null, null, null, [s_gas]));

        Assert.Equal(("Zweit", (Money?)null), (document.Title, document.Amount));
        Assert.Equal([s_gas], document.Tags.Select(tag => tag.TagId));
    }

    [Fact]
    public void Classify_NeverOverridesConfirmedFields()
    {
        var document = Receive();
        document.Edit(s_owner, Fields("Bestätigt"));

        Assert.Throws<InvalidOperationException>(() => document.Classify(Fields("Automatisch")));
        Assert.Equal(("Bestätigt", RecognitionState.Confirmed), (document.Title, document.Recognition));
    }

    [Fact]
    public void Edit_ByTheOwner_SetsTheFields_AsConfirmed()
    {
        var document = Receive();
        document.Classify(Fields("Automatisch", s_strom));

        document.Edit(s_owner, new DocumentFields("Korrigiert", null, null, null, new Money(10m, "EUR"), null, [s_gas]));

        Assert.Equal(("Korrigiert", new Money(10m, "EUR"), RecognitionState.Confirmed), (document.Title, document.Amount, document.Recognition));
        Assert.Equal([s_gas], document.Tags.Select(tag => tag.TagId));
    }

    [Fact]
    public void Edit_KeepsUnchangedTags_AsTheSameInstances()
    {
        var document = Receive();
        document.Classify(Fields(null, s_strom));
        var strom = document.Tags.Single();

        document.Edit(s_owner, Fields(null, s_strom, s_gas));

        Assert.Same(strom, document.Tags[0]);
        Assert.Equal([s_strom, s_gas], document.Tags.Select(tag => tag.TagId));
    }

    [Fact]
    public void Edit_Confirm_AndClearingTheDuplicateHint_NeedTheRightToEdit()
    {
        var document = Receive();
        document.Classify(Fields("Automatisch"));
        document.MarkPossibleDuplicate(Guid.CreateVersion7());

        Assert.Throws<InvalidOperationException>(() => document.Edit(s_other, Fields("Fremd")));
        Assert.Throws<InvalidOperationException>(() => document.Confirm(s_other));
        Assert.Throws<InvalidOperationException>(() => document.ClearPossibleDuplicate(s_other));
        Assert.Equal(("Automatisch", RecognitionState.Automatic), (document.Title, document.Recognition));
        Assert.NotNull(document.PossibleDuplicateOf);
    }

    [Fact]
    public void Confirm_KeepsTheFields_AndMarksThemConfirmed()
    {
        var document = Receive();
        document.Classify(Fields("Automatisch", s_strom));

        document.Confirm(s_owner);

        Assert.Equal(("Automatisch", RecognitionState.Confirmed), (document.Title, document.Recognition));
        Assert.Equal([s_strom], document.Tags.Select(tag => tag.TagId));
    }

    [Fact]
    public void ADuplicateHint_IsSet_AndResolvedByAnEditor()
    {
        var document = Receive();
        var original = Guid.CreateVersion7();

        document.MarkPossibleDuplicate(original);
        Assert.Equal(original, document.PossibleDuplicateOf);

        document.ClearPossibleDuplicate(s_owner);
        Assert.Null(document.PossibleDuplicateOf);
    }

    [Fact]
    public void ADocument_IsNoDuplicateOfItselfOrOfNothing()
    {
        var document = Receive();

        Assert.Throws<ArgumentException>(() => document.MarkPossibleDuplicate(document.Id));
        Assert.Throws<ArgumentException>(() => document.MarkPossibleDuplicate(Guid.Empty));
    }

    private static Document Receive() => Document.Receive("scan.pdf", s_receivedAt, s_owner, isPrivate: false);

    private static DocumentFields Fields(string? title, params Guid[] tagIds) => new(title, null, null, null, null, null, tagIds);
}
