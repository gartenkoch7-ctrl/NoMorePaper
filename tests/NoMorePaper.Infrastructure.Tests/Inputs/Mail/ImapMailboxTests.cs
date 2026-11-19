using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Security;
using NoMorePaper.Infrastructure.Inputs.Mail;
using NoMorePaper.Infrastructure.Security;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>The IMAP mailbox on its own: folders, order, keys, reading what the input needs.</summary>
public sealed class ImapMailboxTests(GreenMailFixture mail) : IClassFixture<GreenMailFixture>
{
    private readonly ISecretProtector _secrets = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionSecretProtector>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Open_CreatesTheFolders_AndListsTheOldestMailsFirst_UpToTheLimit()
    {
        const string box = "box9";
        for (var i = 1; i <= 3; i++)
        {
            await mail.AppendAsync(box, TestMessages.Invoice($"Rechnung {i}"));
        }

        await using var mailbox = await OpenAsync(box);
        var handles = await mailbox.NewMailAsync(2, Ct);

        Assert.Equal(2, handles.Count);
        Assert.True(handles[0].Uid < handles[1].Uid);
        Assert.All(handles, handle => Assert.StartsWith("id:", handle.ItemKey, StringComparison.Ordinal));
        Assert.Equal("Rechnung 1", (await mailbox.ReadAsync(handles[0], Ct))?.Subject);
        Assert.Empty(await mail.SubjectsAsync(box, "NoMorePaper-Fehler"));
        Assert.Empty(await mail.SubjectsAsync(box, "Verarbeitet"));
    }

    [Fact]
    public async Task Read_TurnsHtmlIntoText_TellsInlineFromAttached_AndKeepsTheKeyWhenMovedBack()
    {
        const string box = "box10";
        await mail.AppendAsync(box, TestMessages.Sample("logo-pixel.eml"));

        string key;
        await using (var mailbox = await OpenAsync(box))
        {
            var handle = Assert.Single(await mailbox.NewMailAsync(20, Ct));
            key = handle.ItemKey;
            var content = await mailbox.ReadAsync(handle, Ct);

            Assert.NotNull(content);
            Assert.Equal(("info@funkwelle-mobil.example", SenderCheck.Unknown), (content.From, content.FromCheck));
            Assert.Contains("Kundenportal (https://kundenportal.funkwelle-mobil.example/rechnungen)", content.PlainText, StringComparison.Ordinal);
            Assert.DoesNotContain("color", content.PlainText, StringComparison.Ordinal);
            Assert.Equal([("image/png", false, 1), ("image/gif", false, (int?)null)], content.Attachments.Select(part => (part.DeclaredType, part.IsAttachment, part.PixelWidth)));
            Assert.All(content.Attachments, part => Assert.Null(MailIntake.DocumentType(part)));
            await mailbox.MoveAsync(handle, MailFolder.Spam, Ct);
        }

        await mail.MoveBackAsync(box, "NoMorePaper-Spam");
        await using (var again = await OpenAsync(box))
        {
            Assert.Equal(key, Assert.Single(await again.NewMailAsync(20, Ct)).ItemKey);
        }
    }

    [Fact]
    public async Task Read_TakesTheVerdictOfTheTrustedServerOnly()
    {
        const string box = "box11";
        await mail.AppendAsync(box, TestMessages.Invoice("Echt", authenticationResults: $"{GreenMailFixture.AuthServId}; dmarc=pass header.from=stadtwerke.example"));
        await mail.AppendAsync(box, TestMessages.Invoice("Fremd", authenticationResults: "mx.other.example; dmarc=pass header.from=stadtwerke.example"));

        await using var mailbox = await OpenAsync(box);
        var checks = new List<SenderCheck>();
        foreach (var handle in await mailbox.NewMailAsync(20, Ct))
        {
            checks.Add((await mailbox.ReadAsync(handle, Ct))!.FromCheck);
        }

        Assert.Equal([SenderCheck.Passed, SenderCheck.Unknown], checks);
    }

    [Fact]
    public async Task TheProbe_TellsSignedInFromRefusedFromUnreachable()
    {
        var probe = new ImapMailboxProbe(new ImapMailboxFactory(_secrets, TimeProvider.System, NullLoggerFactory.Instance), TimeProvider.System, NullLogger<ImapMailboxProbe>.Instance);
        var settings = mail.Settings("box12");

        Assert.Equal(MailboxProbeResult.Ok, await probe.ProbeAsync(settings, GreenMailFixture.Password, Ct));
        Assert.Equal(MailboxProbeResult.SignInFailed, await probe.ProbeAsync(settings, "falsch", Ct));
        Assert.Equal(MailboxProbeResult.Unreachable, await probe.ProbeAsync(settings with { Host = "127.0.0.1", Port = 1 }, GreenMailFixture.Password, Ct));
        Assert.Equal(MailboxProbeResult.Unreachable, await probe.ProbeAsync(settings with { Security = MailboxSecurity.Ssl }, GreenMailFixture.Password, Ct));
    }

    private Task<ImapMailbox> OpenAsync(string box) =>
        new ImapMailboxFactory(_secrets, TimeProvider.System, NullLoggerFactory.Instance)
            .OpenAsync(mail.Settings(box), _secrets.Protect(SecretPurposes.MailPassword, GreenMailFixture.Password), Ct);
}
