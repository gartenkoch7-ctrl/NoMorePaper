using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>Answers every mail alike; keeps what it was asked.</summary>
internal sealed class FakeMailAssessor : IMailAssessor
{
    public MailAssessment Answer { get; set; } = new(false, false, "Zahlbar bis 30.11.");

    /// <summary>Thrown instead of answering while set.</summary>
    public Exception? Failure { get; set; }

    public List<(MailText Mail, bool KnownSender)> Asked { get; } = [];

    public Task<MailAssessment> AssessAsync(MailText mail, bool knownSender, CancellationToken cancellationToken)
    {
        Asked.Add((mail, knownSender));
        if (Failure is not null)
        {
            throw Failure;
        }

        // A known sender is never spam, whatever the model says.
        return Task.FromResult(knownSender ? Answer with { IsSpam = false } : Answer);
    }
}
