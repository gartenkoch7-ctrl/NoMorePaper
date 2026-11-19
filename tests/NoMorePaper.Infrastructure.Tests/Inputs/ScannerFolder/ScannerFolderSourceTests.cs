using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Inputs.ScannerFolder;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>The scanner folder against a real file system and Postgres: owner, privacy, finished files, deleting, errors.</summary>
[Collection(SharedPostgres.Name)]
public sealed class ScannerFolderSourceTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AHalfWrittenFile_IsTaken_OnlyOnceItStoppedGrowing()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_growing");
        var admin = await database.AddAdminAsync();
        using var scanner = new ScannerFolderHarness(database);
        var path = scanner.Write("admin/scan.pdf", ScannerFolderHarness.Pdf("page 1"));

        Assert.Equal(0, (await scanner.RunAsync(Ct)).Received);
        await File.AppendAllTextAsync(path, "page 2", Ct);
        Assert.Equal(0, (await scanner.RunAsync(Ct)).Received);
        Assert.Equal(["admin/scan.pdf"], scanner.Files());

        var result = await scanner.RunAsync(Ct);

        Assert.Equal(InputRunResult.Nothing with { Received = 1 }, result);
        Assert.Empty(scanner.Files());
        var document = await database.Db().Documents.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((admin.Id, DocumentSource.ScannerFolder, false), (document.OwnerId!.Value, document.Source, document.NeedsOwnerCheck));
    }

    [Fact]
    public async Task AnAccountsFolder_GivesTheAccountItsDocuments_WithItsPreference()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_accounts");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "Bob");
        await new EfAccountPreferencesStore(database.Db()).SaveAsync(bob.Id, new AccountPreferences(true, []), Ct);
        using var scanner = new ScannerFolderHarness(database);
        await scanner.RunAsync(Ct);
        scanner.Write("bob/brief.pdf", ScannerFolderHarness.Pdf("bob"));
        scanner.Write("admin/brief.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]);

        var result = await scanner.RunTwiceAsync(Ct);

        Assert.Equal((2, 0, (InputProblem?)null), (result.Received, result.Skipped, result.Problem));
        Assert.Empty(result.Accounts);
        var documents = await Documents(database);
        Assert.Contains((bob.Id, true, false), documents);
        Assert.Contains((admin.Id, false, false), documents);
        Assert.Empty(scanner.Files());
    }

    [Fact]
    public async Task TheRoot_GoesToTheAdministrator_Privately_ForTheOwnerCheck()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_root");
        var admin = await database.AddAdminAsync();
        using var scanner = new ScannerFolderHarness(database);
        scanner.Write("Scan 2026-11-19.pdf", ScannerFolderHarness.Pdf("root"));

        Assert.Equal(1, (await scanner.RunTwiceAsync(Ct)).Received);

        Assert.Equal([(admin.Id, true, true)], await Documents(database));
        Assert.Empty(scanner.Files());
    }

    [Fact]
    public async Task AFolderWithoutAccount_KeepsItsFiles_AndIsReportedForTheWholeInput_AlsoAfterARestart()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_unknown");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        using var scanner = new ScannerFolderHarness(database);
        await scanner.RunAsync(Ct);
        scanner.Write("bob/brief.pdf", ScannerFolderHarness.Pdf("bob"));
        scanner.Write("carol/brief.pdf", ScannerFolderHarness.Pdf("carol"));
        Assert.True(await database.Store().RemoveAsync(bob.Id, Ct));

        var result = await scanner.RunTwiceAsync(Ct);
        scanner.Stable = new StableFiles();
        var afterRestart = await scanner.RunTwiceAsync(Ct);

        var expected = InputRunResult.Failed(new InputProblem(InputProblemKind.UnknownFolder, NeedsAttention: true));
        Assert.Equal((expected, expected), (result, afterRestart));
        Assert.Empty(result.Accounts);
        Assert.Equal(["bob/brief.pdf", "carol/brief.pdf"], scanner.Files());
        Assert.Empty(await Documents(database));
    }

    [Fact]
    public async Task FilesOfUnknownType_GoToTheErrorFolder_UnderTheirOwnName()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_errors");
        await database.AddAdminAsync();
        using var scanner = new ScannerFolderHarness(database);
        scanner.Write("admin/notiz.txt", "Hallo"u8.ToArray());
        scanner.Write("notiz.txt", "Hallo"u8.ToArray());
        scanner.Write("admin/Rechnung ä..😀.docx", "PK"u8.ToArray());
        scanner.Write("admin/Rechnung ä..😀.pdf", ScannerFolderHarness.Pdf("unicode"));

        var result = await scanner.RunTwiceAsync(Ct);

        Assert.Equal((1, 3, (InputProblem?)null), (result.Received, result.Skipped, result.Problem));
        Assert.Equal(
            ["Fehler/20261119-143005-1-notiz.txt", "Fehler/20261119-143005-Rechnung ä..😀.docx", "Fehler/20261119-143005-notiz.txt"],
            scanner.Files());
    }

    [Fact]
    public async Task HiddenFiles_LockFiles_AndDeeperFolders_StayUntouched()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_ignored");
        await database.AddAdminAsync();
        using var scanner = new ScannerFolderHarness(database);
        scanner.Write("admin/.DS_Store", [1, 2]);
        scanner.Write("admin/~$brief.pdf", ScannerFolderHarness.Pdf("lock"));
        scanner.Write("admin/2026/brief.pdf", ScannerFolderHarness.Pdf("deeper"));
        scanner.Write(".hidden/brief.pdf", ScannerFolderHarness.Pdf("hidden"));

        Assert.Equal(InputRunResult.Nothing, await scanner.RunTwiceAsync(Ct));

        Assert.Equal([".hidden/brief.pdf", "admin/.DS_Store", "admin/2026/brief.pdf", "admin/~$brief.pdf"], scanner.Files());
    }

    [Fact]
    public async Task AFileTheOwnerHasAlready_IsDeleted_WithoutASecondDocument()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_duplicate");
        await database.AddAdminAsync();
        using var scanner = new ScannerFolderHarness(database);
        scanner.Write("admin/a.pdf", ScannerFolderHarness.Pdf("same"));
        await scanner.RunTwiceAsync(Ct);
        scanner.Write("admin/again.pdf", ScannerFolderHarness.Pdf("same"));

        var result = await scanner.RunTwiceAsync(Ct);

        Assert.Equal((0, 1), (result.Received, result.Skipped));
        Assert.Empty(scanner.Files());
        Assert.Single(await Documents(database));
    }

    [Fact]
    public async Task AnotherAccountsSharedCopy_DoesNotStopTheFile()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_shared");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        using var scanner = new ScannerFolderHarness(database);
        await scanner.RunAsync(Ct);
        scanner.Write("admin/a.pdf", ScannerFolderHarness.Pdf("same"));
        await scanner.RunTwiceAsync(Ct);
        scanner.Write("bob/a.pdf", ScannerFolderHarness.Pdf("same"));

        Assert.Equal(1, (await scanner.RunTwiceAsync(Ct)).Received);

        Assert.Equal([admin.Id, bob.Id], (await Documents(database)).Select(document => document.Owner).Order());
    }

    [Fact]
    public async Task AFailedCommit_LeavesTheFile_UntilARunGetsThrough()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_commit");
        await database.AddAdminAsync();
        using var scanner = new ScannerFolderHarness(database) { Repository = repository => new FailingCommit(repository) };
        scanner.Write("admin/brief.pdf", ScannerFolderHarness.Pdf("brief"));

        var failed = await scanner.RunTwiceAsync(Ct);

        Assert.Equal(InputRunResult.Failed(new InputProblem(InputProblemKind.Failed)), failed);
        Assert.Equal(["admin/brief.pdf"], scanner.Files());
        Assert.Empty(await Documents(database));

        scanner.Repository = repository => repository;
        Assert.Equal(1, (await scanner.RunAsync(Ct)).Received);
        Assert.Empty(scanner.Files());
    }

    [Fact]
    public async Task AMissingScannerFolder_IsReportedAsUnreachable()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "scanner_missing");
        using var scanner = new ScannerFolderHarness(database);
        Directory.Delete(scanner.Root);

        var result = await scanner.RunAsync(Ct);

        Directory.CreateDirectory(scanner.Root);
        Assert.Equal(InputRunResult.Failed(new InputProblem(InputProblemKind.Unreachable, NeedsAttention: true)), result);
    }

    private static async Task<List<(Guid Owner, bool IsPrivate, bool NeedsOwnerCheck)>> Documents(AccountDatabase database) =>
        (await database.Db().Documents.AsNoTracking().ToListAsync(Ct))
            .Select(document => (document.OwnerId!.Value, document.IsPrivate, document.NeedsOwnerCheck))
            .ToList();
}
