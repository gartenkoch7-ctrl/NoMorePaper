using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary><see cref="ProcessMailbox"/> with fakes around the real <see cref="MailIntake"/> and <see cref="IntakeDocument"/>.</summary>
internal sealed class MailHarness
{
    public const string Sender = "rechnung@stadtwerke.example";

    public const string Subject = "Ihre Rechnung November";

    public FakeMailbox Mailbox { get; } = new();

    public FakeMailAssessor Assessor { get; } = new();

    public FakeSeenItems Seen { get; } = new();

    public FakeDocumentRepository Documents { get; } = new();

    public FakeDocumentStorage Storage { get; } = new();

    public FakeAccountStore Accounts { get; } = new();

    public FakeAccountPreferencesStore Preferences { get; } = new();

    public FakePdfComposer Pdf { get; } = new();

    public RecordingLogger<ProcessMailbox> Log { get; } = new();

    public RecordingLogger<MailIntake> IntakeLog { get; } = new();

    public RecordingLogger<IntakeDocument> DocumentLog { get; } = new();

    /// <summary>The clock of the run; <see cref="SteppingClock.Step"/> 0 stands still.</summary>
    public SteppingClock Clock { get; } = new();

    public IEnumerable<string> AllMessages => Log.Messages.Concat(IntakeLog.Messages).Concat(DocumentLog.Messages);

    public static MailAttachment PdfAttachment(string name = "Rechnung 2026-11.pdf") =>
        new(name, "application/pdf", true, "%PDF-1.7\n1 0 obj"u8.ToArray(), null, null);

    public static MailContent Mail(SenderCheck check = SenderCheck.Unknown, params MailAttachment[] attachments) =>
        new(Sender, check, Subject, new DateTimeOffset(2026, 11, 19, 8, 30, 0, TimeSpan.FromHours(1)), "Guten Tag, anbei die Rechnung. Zahlbar bis 30.11.", attachments);

    public ProcessMailbox Process() => new(Seen, Preferences, Assessor, Intake(), Clock, Log);

    public MailIntake Intake() =>
        new(new IntakeDocument(Documents, Storage, Accounts, Preferences, TimeProvider.System, DocumentLog), Pdf, IntakeLog);

    public Task<Application.Inputs.InputRunResult> RunAsync() => Process().RunAsync(Mailbox, TestContext.Current.CancellationToken);
}
