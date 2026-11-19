using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Inputs.ScannerFolder;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Documents;
using NoMorePaper.Infrastructure.Tests.Processing;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>
/// A scanner folder and a document storage on disk, the intake against Postgres. Every <see cref="RunAsync"/>
/// builds the source anew from fresh scopes, like the scheduler; only <see cref="Stable"/> spans the runs.
/// </summary>
internal sealed class ScannerFolderHarness(AccountDatabase database) : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nomorepaper-scan-");
    private readonly DirectoryInfo _storage = Directory.CreateTempSubdirectory("nomorepaper-scan-store-");

    public string Root => _root.FullName;

    public StableFiles Stable { get; set; } = new();

    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 19, 14, 30, 5, TimeSpan.Zero));

    /// <summary>Replaces the repository the intake uses, e.g. with one whose commit fails.</summary>
    public Func<IDocumentRepository, IDocumentRepository> Repository { get; set; } = repository => repository;

    public static byte[] Pdf(string marker) => [.. "%PDF-1.7\n"u8, .. System.Text.Encoding.UTF8.GetBytes(marker)];

    public string Write(string relativePath, byte[] content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>Runs twice, as a finished file needs two runs that see it unchanged.</summary>
    public async Task<InputRunResult> RunTwiceAsync(CancellationToken cancellationToken)
    {
        await RunAsync(cancellationToken);
        return await RunAsync(cancellationToken);
    }

    public Task<InputRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var db = database.Db();
        var intake = new IntakeDocument(
            Repository(new EfDocumentRepository(db)),
            new FileSystemDocumentStorage(_storage.FullName, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance),
            database.Store(),
            new EfAccountPreferencesStore(db),
            Clock,
            NullLogger<IntakeDocument>.Instance);
        var source = new ScannerFolderSource(
            new ScannerFolderSettings(Root),
            new AccountFolders(database.Store(), NullLogger<AccountFolders>.Instance),
            Stable,
            new ScannerFileIntake(intake, Clock, NullLogger<ScannerFileIntake>.Instance),
            NullLogger<ScannerFolderSource>.Instance);
        return source.RunAsync(cancellationToken);
    }

    /// <summary>The files left in the scanner folder, relative and with forward slashes.</summary>
    public string[] Files() =>
        [.. Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    public void Dispose()
    {
        _root.Delete(recursive: true);
        _storage.Delete(recursive: true);
    }
}
