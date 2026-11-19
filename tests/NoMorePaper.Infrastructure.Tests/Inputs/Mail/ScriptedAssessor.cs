using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>Stands in for the LLM: spam when the subject starts with "SPAM", otherwise a document with a note.</summary>
internal sealed class ScriptedAssessor : IMailAssessor
{
    public List<(string Subject, bool KnownSender)> Asked { get; } = [];

    /// <summary>Answers mails without attachment as no document while set.</summary>
    public bool NothingIsADocument { get; set; }

    /// <summary>Runs before each answer, e.g. to break the mailbox while the run is going on.</summary>
    public Func<Task>? BeforeAnswer { get; set; }

    public async Task<MailAssessment> AssessAsync(MailText mail, bool knownSender, CancellationToken cancellationToken)
    {
        Asked.Add((mail.Subject, knownSender));
        if (BeforeAnswer is not null)
        {
            await BeforeAnswer();
        }

        var spam = !knownSender && mail.Subject.StartsWith("SPAM", StringComparison.Ordinal);
        return new MailAssessment(spam, !spam && !NothingIsADocument, spam ? null : "Zahlung bis 30.11. per Lastschrift.");
    }
}
