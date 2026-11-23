using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Design;
using NoMorePaper.Web.Client.Documents;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Documents;

public sealed class DocumentStatusTests
{
    [Theory]
    [InlineData(DocumentProcessingState.Queued, StatusKind.Waiting)]
    [InlineData(DocumentProcessingState.Processing, StatusKind.Processing)]
    [InlineData(DocumentProcessingState.Done, StatusKind.Done)]
    [InlineData(DocumentProcessingState.Failed, StatusKind.Failed)]
    public void KindOf_MapsEveryState(DocumentProcessingState state, StatusKind expected) =>
        Assert.Equal(expected, DocumentStatus.KindOf(state));

    [Fact]
    public void KindOf_UnknownState_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentStatus.KindOf((DocumentProcessingState)99));

    [Theory]
    [InlineData(StatusKind.Waiting, "StatusWaiting")]
    [InlineData(StatusKind.Processing, "StatusProcessing")]
    [InlineData(StatusKind.Done, "StatusDone")]
    [InlineData(StatusKind.Failed, "StatusFailed")]
    [InlineData(StatusKind.Recognized, "StatusRecognized")]
    public void StatusKeys_NameTheWordOfEveryKind(StatusKind kind, string expected) =>
        Assert.Equal(expected, StatusKeys.For(kind));
}
