using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Security;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Documents.Pdf;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Inputs.Mail;
using NoMorePaper.Infrastructure.Security;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Documents;
using NoMorePaper.Infrastructure.Tests.Persistence;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>
/// The whole mail input against GreenMail and Postgres: <see cref="MailSource"/> with the real stores, intake,
/// PDF composer and IMAP mailbox; only the LLM is scripted (<see cref="Assessor"/>).
/// </summary>
internal sealed class MailInput : IAsyncDisposable
{
    private readonly GreenMailFixture _mail;
    private readonly DirectoryInfo _storage = Directory.CreateTempSubdirectory("nomorepaper-mail-");
    private readonly ISecretProtector _secrets = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionSecretProtector>.Instance);

    private MailInput(AccountDatabase database, GreenMailFixture mail, string box)
    {
        Database = database;
        _mail = mail;
        Box = box;
    }

    public AccountDatabase Database { get; }

    public string Box { get; }

    public Account Admin { get; private set; } = null!;

    public ScriptedAssessor Assessor { get; } = new();

    /// <summary>Storing files fails while set, as with a full disk.</summary>
    public bool StorageBroken { get; set; }

    public static async Task<MailInput> CreateAsync(PostgresFixture postgres, GreenMailFixture mail, string database, string box)
    {
        var input = new MailInput(await AccountDatabase.CreateAsync(postgres, database), mail, box);
        input.Admin = await input.Database.AddAdminAsync();
        return input;
    }

    public async Task ConnectAsync(string password = GreenMailFixture.Password, MailboxSettings? settings = null)
    {
        var connection = new InputConnection(
            InputKind.Mail, null, (settings ?? _mail.Settings(Box)).ToJson(), _secrets.Protect(SecretPurposes.MailPassword, password), DateTimeOffset.UtcNow);
        await new EfInputConnections(Database.Db()).SaveAsync(connection, CancellationToken.None);
    }

    /// <summary>A new source as the scheduler builds it for each run, with its own scope.</summary>
    public Task<InputRunResult> RunAsync()
    {
        var db = Database.Db();
        var storage = StorageBroken
            ? new FileSystemDocumentStorage(Path.Combine(_storage.FullName, "not-a-folder.txt"), new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance)
            : new FileSystemDocumentStorage(_storage.FullName, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
        File.WriteAllText(Path.Combine(_storage.FullName, "not-a-folder.txt"), "x");
        var intake = new IntakeDocument(
            new EfDocumentRepository(db), storage, Database.Store(), new EfAccountPreferencesStore(db), TimeProvider.System, NullLogger<IntakeDocument>.Instance);
        var process = new ProcessMailbox(
            new EfSeenItems(db),
            new EfAccountPreferencesStore(db),
            Assessor,
            new MailIntake(intake, new PdfSharpComposer(TimeProvider.System, NullLogger<PdfSharpComposer>.Instance), NullLogger<MailIntake>.Instance),
            TimeProvider.System,
            NullLogger<ProcessMailbox>.Instance);
        var source = new MailSource(
            new EfInputConnections(db),
            new ImapMailboxFactory(_secrets, TimeProvider.System, NullLoggerFactory.Instance),
            process,
            NullLogger<MailSource>.Instance);
        return source.RunAsync(CancellationToken.None);
    }

    public Task<List<Document>> DocumentsAsync() =>
        Database.Db().Documents.AsNoTracking().OrderBy(document => document.ReceivedAt).ToListAsync();

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        _storage.Delete(recursive: true);
    }
}
