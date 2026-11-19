using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>Who owns a mail, and what the source reports when the mailbox cannot be used.</summary>
[Collection(SharedPostgres.Name)]
public sealed class MailSenderTests(PostgresFixture postgres, GreenMailFixture mail) : IClassFixture<GreenMailFixture>
{
    private const string Anna = "anna@familie.example";

    [Fact]
    public async Task AnAccountsAddress_CountsOnlyWithThePassOfTheOwnServer()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_sender", "box6");
        var bob = await input.Database.AddMemberAsync(input.Admin.Id, "bob");
        await new EfAccountPreferencesStore(input.Database.Db()).SaveAsync(bob.Id, new AccountPreferences(false, [Anna]), CancellationToken.None);
        await input.ConnectAsync();

        // A sender can write any header: the verdict of another server does not count.
        await mail.AppendAsync(input.Box, TestMessages.Invoice("SPAM gefaelscht", Anna, authenticationResults: "mx.evil.example; dmarc=pass header.from=familie.example"));
        await mail.AppendAsync(input.Box, TestMessages.Invoice("SPAM echt", Anna, authenticationResults: $"{GreenMailFixture.AuthServId}; spf=pass smtp.mailfrom={Anna}; dmarc=pass header.from=familie.example"));

        await input.RunAsync();

        Assert.Equal([("SPAM gefaelscht", false), ("SPAM echt", true)], input.Assessor.Asked);
        var document = Assert.Single(await input.DocumentsAsync());
        Assert.Equal((bob.Id, false), (document.OwnerId, document.NeedsOwnerCheck));
        Assert.Equal(["SPAM gefaelscht"], await mail.SubjectsAsync(input.Box, "NoMorePaper-Spam"));
    }

    [Fact]
    public async Task WithoutSetUp_TheInputIsNotConfigured()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_none", "box7");

        Assert.Equal(InputRunResult.Failed(new InputProblem(InputProblemKind.NotConfigured)), await input.RunAsync());
    }

    [Fact]
    public async Task AWrongPassword_NeedsAttention_AClosedPortIsUnreachable()
    {
        await using var input = await MailInput.CreateAsync(postgres, mail, "mail_refused", "box8");
        await mail.AppendAsync(input.Box, TestMessages.Invoice("Rechnung bleibt"));

        await input.ConnectAsync(password: "falsch");
        Assert.Equal(new InputProblem(InputProblemKind.SignInFailed, NeedsAttention: true), (await input.RunAsync()).Problem);

        await input.ConnectAsync(settings: new MailboxSettings("127.0.0.1", 1, MailboxSecurity.None, "box8", null));
        Assert.Equal(new InputProblem(InputProblemKind.Unreachable), (await input.RunAsync()).Problem);

        Assert.Equal(["Rechnung bleibt"], await mail.SubjectsAsync(input.Box, "INBOX"));
        Assert.Empty(input.Assessor.Asked);
    }
}
