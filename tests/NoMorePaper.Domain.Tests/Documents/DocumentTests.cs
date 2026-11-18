using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset s_receivedAt = new(2026, 11, 18, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();

    [Fact]
    public void Receive_CreatesAQueuedDocument_WithAVersion7IdFromTheGivenTime()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);

        Assert.Equal(7, document.Id.Version);
        Assert.Equal(s_receivedAt.ToUnixTimeMilliseconds(), UnixMillisecondsOf(document.Id));
        Assert.Equal("Letter", document.Title);
        Assert.Equal(s_receivedAt, document.ReceivedAt);
        Assert.Equal(ProcessingState.Queued, document.State);
        Assert.Null(document.ContentText);
        Assert.Null(document.FailureReason);
        Assert.Empty(document.Files);
    }

    [Fact]
    public void Receive_StoresTheTimeInUtc()
    {
        var document = Document.Receive("Letter", s_receivedAt.ToOffset(TimeSpan.FromHours(1)), s_owner, isPrivate: false);

        Assert.Equal(TimeSpan.Zero, document.ReceivedAt.Offset);
        Assert.Equal(s_receivedAt, document.ReceivedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Receive_RejectsAnEmptyTitle(string title) =>
        Assert.ThrowsAny<ArgumentException>(() => Document.Receive(title, s_receivedAt, s_owner, isPrivate: false));

    [Fact]
    public void Receive_RejectsATooLongTitle()
    {
        Document.Receive(new string('a', Document.MaxTitleLength), s_receivedAt, s_owner, isPrivate: false);

        Assert.Throws<ArgumentException>(() => Document.Receive(new string('a', Document.MaxTitleLength + 1), s_receivedAt, s_owner, isPrivate: false));
    }

    [Fact]
    public void Complete_AfterStart_IsDone_AndKeepsTheText()
    {
        var document = Started();

        document.Complete("Rechnung");

        Assert.Equal(ProcessingState.Done, document.State);
        Assert.Equal("Rechnung", document.ContentText);
    }

    [Fact]
    public void Complete_AcceptsAnEmptyText_ForDocumentsWithoutText()
    {
        var document = Started();

        document.Complete(string.Empty);

        Assert.Equal(ProcessingState.Done, document.State);
        Assert.Equal(string.Empty, document.ContentText);
    }

    [Fact]
    public void Fail_AfterStart_IsFailed_WithTheReason()
    {
        var document = Started();

        document.Fail("OCR timed out");

        Assert.Equal(ProcessingState.Failed, document.State);
        Assert.Equal("OCR timed out", document.FailureReason);
    }

    [Fact]
    public void Fail_CutsAVeryLongReason()
    {
        var document = Started();

        document.Fail(new string('x', Document.MaxFailureReasonLength + 50));

        Assert.Equal(Document.MaxFailureReasonLength, document.FailureReason!.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fail_RequiresAReason(string reason) =>
        Assert.ThrowsAny<ArgumentException>(() => Started().Fail(reason));

    [Fact]
    public void StartProcessing_AgainWhileProcessing_IsAllowed_ForRetries()
    {
        var document = Started();

        document.StartProcessing();

        Assert.Equal(ProcessingState.Processing, document.State);
    }

    [Theory]
    [InlineData(ProcessingState.Done)]
    [InlineData(ProcessingState.Failed)]
    public void StartProcessing_AfterTheEnd_IsRejected(ProcessingState end) =>
        Assert.Throws<InvalidOperationException>(() => InState(end).StartProcessing());

    [Theory]
    [InlineData(ProcessingState.Queued)]
    [InlineData(ProcessingState.Done)]
    [InlineData(ProcessingState.Failed)]
    public void Complete_OutsideProcessing_IsRejected(ProcessingState state) =>
        Assert.Throws<InvalidOperationException>(() => InState(state).Complete("text"));

    [Theory]
    [InlineData(ProcessingState.Queued)]
    [InlineData(ProcessingState.Done)]
    [InlineData(ProcessingState.Failed)]
    public void Fail_OutsideProcessing_IsRejected(ProcessingState state) =>
        Assert.Throws<InvalidOperationException>(() => InState(state).Fail("reason"));

    [Fact]
    public void Complete_RejectsNullText() =>
        Assert.Throws<ArgumentNullException>(() => Started().Complete(null!));

    [Fact]
    public void AttachFile_AddsOneFilePerKind()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);
        var original = DocumentFileTests.File(DocumentFileKind.Original);
        var archive = DocumentFileTests.File(DocumentFileKind.Archive);

        document.AttachFile(original);
        document.AttachFile(archive);

        Assert.Same(original, document.FindFile(DocumentFileKind.Original));
        Assert.Same(archive, document.FindFile(DocumentFileKind.Archive));
        Assert.Equal(2, document.Files.Count);
    }

    [Fact]
    public void AttachFile_RejectsASecondFileOfTheSameKind()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);
        document.AttachFile(DocumentFileTests.File(DocumentFileKind.Original));

        Assert.Throws<InvalidOperationException>(() => document.AttachFile(DocumentFileTests.File(DocumentFileKind.Original)));
        Assert.Single(document.Files);
    }

    [Fact]
    public void FindFile_IsNull_WhenTheKindIsMissing() =>
        Assert.Null(Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false).FindFile(DocumentFileKind.Archive));

    private static Document Started()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);
        document.StartProcessing();
        return document;
    }

    private static Document InState(ProcessingState state)
    {
        if (state == ProcessingState.Queued)
        {
            return Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);
        }

        var document = Started();
        if (state == ProcessingState.Done)
        {
            document.Complete("text");
        }
        else if (state == ProcessingState.Failed)
        {
            document.Fail("reason");
        }

        return document;
    }

    // RFC 9562: the first 48 bits of a version 7 UUID are the Unix time in milliseconds.
    private static long UnixMillisecondsOf(Guid id)
    {
        var bytes = id.ToByteArray(bigEndian: true);
        return bytes.Take(6).Aggregate(0L, (value, b) => (value << 8) | b);
    }
}
