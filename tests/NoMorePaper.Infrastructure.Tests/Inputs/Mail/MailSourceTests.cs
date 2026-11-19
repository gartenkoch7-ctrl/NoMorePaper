using NoMorePaper.Application.Inputs;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>The mail input end to end against GreenMail and Postgres: nothing lost, nothing taken twice (ADR-0024).</summary>
[Collection(SharedPostgres.Name)]
public sealed class MailSourceTests(PostgresFixture postgres, GreenMailFixture mail) : IClassFixture<GreenMailFixture>
{
    private const string Processed = "Verarbeitet";
    private const string Spam = "NoMorePaper-Spam";
    private const string Failed = "NoMorePaper-Fehler";

    [Fact]
    public async Task AnAttachment_BecomesADocumentOfTheAdministrator_AndTheMailMovesToProcessed()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_attachment", "box1");
        await input.ConnectAsync();
        await mail.AppendAsync(input.Box, TestMessages.Invoice("Rechnung November"));

        var result = await input.RunAsync();

        Assert.Equal(new InputRunResult(1, 0), result);
        var document = Assert.Single(await input.DocumentsAsync());
        Assert.Equal((input.Admin.Id, true, true, DocumentSource.Mail), (document.OwnerId, document.IsPrivate, document.NeedsOwnerCheck, document.Source));
        Assert.Equal(new MailOrigin(TestMessages.Sender, "Rechnung November", "Zahlung bis 30.11. per Lastschrift."), document.MailOrigin);
        Assert.Equal("Rechnung.pdf", Assert.Single(document.Files).FileName);
        Assert.Empty(await mail.SubjectsAsync(input.Box, "INBOX"));
        Assert.Equal(["Rechnung November"], await mail.SubjectsAsync(input.Box, Processed));
        Assert.Empty(await mail.SubjectsAsync(input.Box, Spam));
    }

    [Fact]
    public async Task Spam_GoesToItsFolder_AndMovedBack_IsTakenWithoutCheck()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_spam", "box2");
        await input.ConnectAsync();
        await mail.AppendAsync(input.Box, TestMessages.Invoice("SPAM Sie haben gewonnen", pdf: false));

        Assert.Equal(new InputRunResult(0, 1), await input.RunAsync());
        Assert.Equal(["SPAM Sie haben gewonnen"], await mail.SubjectsAsync(input.Box, Spam));
        Assert.Empty(await input.DocumentsAsync());

        await mail.MoveBackAsync(input.Box, Spam);
        Assert.Equal(new InputRunResult(1, 0), await input.RunAsync());

        Assert.Equal([false, true], input.Assessor.Asked.Select(asked => asked.KnownSender));
        var document = Assert.Single(await input.DocumentsAsync());
        Assert.Equal(("Mail 2026-11-19.pdf", "application/pdf"), (document.Files[0].FileName, document.Files[0].MediaType));
        Assert.Equal(["SPAM Sie haben gewonnen"], await mail.SubjectsAsync(input.Box, Processed));
    }

    [Fact]
    public async Task AFailingIntake_LeavesTheMailInTheInbox_AndAfterThreeRunsPutsItAside()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_failing", "box3");
        await input.ConnectAsync();
        await mail.AppendAsync(input.Box, TestMessages.Invoice("Rechnung mit Fehler"));
        input.StorageBroken = true;

        Assert.Null((await input.RunAsync()).Problem);
        Assert.Null((await input.RunAsync()).Problem);
        Assert.Equal(["Rechnung mit Fehler"], await mail.SubjectsAsync(input.Box, "INBOX"));
        var third = await input.RunAsync();

        Assert.Equal(new InputProblem(InputProblemKind.StuckItems, NeedsAttention: true), third.Problem);
        Assert.Equal(["Rechnung mit Fehler"], await mail.SubjectsAsync(input.Box, Failed));
        Assert.Empty(await input.DocumentsAsync());

        // Moved back after the disk was fixed, it gets three new tries and goes through.
        input.StorageBroken = false;
        await mail.MoveBackAsync(input.Box, Failed);
        Assert.Equal(1, (await input.RunAsync()).Received);
        Assert.Single(await input.DocumentsAsync());
    }

    [Fact]
    public async Task AFailedMove_LeavesOnlyTheMove_TheNextRunAddsNoSecondDocument()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_move", "box4");
        await input.ConnectAsync();
        await mail.AppendAsync(input.Box, TestMessages.Invoice("Rechnung Umzug"));
        input.Assessor.BeforeAnswer = () => mail.DeleteFolderAsync(input.Box, Processed);

        Assert.Equal(1, (await input.RunAsync()).Received);
        Assert.Equal(["Rechnung Umzug"], await mail.SubjectsAsync(input.Box, "INBOX"));

        input.Assessor.BeforeAnswer = null;
        Assert.Equal(new InputRunResult(0, 0), await input.RunAsync());

        Assert.Single(await input.DocumentsAsync());
        Assert.Single(input.Assessor.Asked);
        Assert.Equal(["Rechnung Umzug"], await mail.SubjectsAsync(input.Box, Processed));
    }

    [Fact]
    public async Task LogoAndTrackingPixel_BecomeNoDocument_APhotographedPageDoes()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_images", "box5");
        await input.ConnectAsync();
        input.Assessor.NothingIsADocument = true;
        await mail.AppendAsync(input.Box, TestMessages.Sample("logo-pixel.eml"));
        await mail.AppendAsync(input.Box, TestMessages.Photo("Foto vom Brief"));

        Assert.Equal(new InputRunResult(1, 1), await input.RunAsync());

        var document = Assert.Single(await input.DocumentsAsync());
        Assert.Equal(("Brief.jpg", "image/jpeg"), (document.Files[0].FileName, document.Files[0].MediaType));
        Assert.Equal(2, (await mail.SubjectsAsync(input.Box, Processed)).Count);
    }
}
