using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class DocumentPrivacyTests
{
    private static readonly DateTimeOffset s_receivedAt = new(2026, 11, 18, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly Guid s_other = Guid.CreateVersion7();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Receive_RecordsOwnerAndPrivacy(bool isPrivate)
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate);

        Assert.Equal((s_owner, isPrivate), (document.OwnerId, document.IsPrivate));
    }

    [Fact]
    public void Receive_RequiresAnOwner() =>
        Assert.Throws<ArgumentException>(() => Document.Receive("Letter", s_receivedAt, Guid.Empty, isPrivate: false));

    [Fact]
    public void APrivateDocument_IsVisibleToItsOwnerOnly()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: true);

        Assert.True(document.IsVisibleTo(s_owner));
        Assert.False(document.IsVisibleTo(s_other));
    }

    [Fact]
    public void ASharedDocument_IsVisibleToEverybody()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);

        Assert.True(document.IsVisibleTo(s_owner));
        Assert.True(document.IsVisibleTo(s_other));
    }

    [Fact]
    public void TheOwner_CanMakeItPrivate_AndShareItAgain()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: false);

        document.MakePrivate(s_owner);
        Assert.True(document.IsPrivate);

        document.Share(s_owner);
        Assert.False(document.IsPrivate);
    }

    [Fact]
    public void Nobody_ButTheOwner_ChangesWhoSeesIt()
    {
        var document = Document.Receive("Letter", s_receivedAt, s_owner, isPrivate: true);

        Assert.Throws<InvalidOperationException>(() => document.Share(s_other));
        Assert.Throws<InvalidOperationException>(() => document.MakePrivate(s_other));
        Assert.True(document.IsPrivate);
    }
}
