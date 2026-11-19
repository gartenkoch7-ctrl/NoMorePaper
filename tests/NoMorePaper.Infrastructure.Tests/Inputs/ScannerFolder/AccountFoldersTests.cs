using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Inputs.ScannerFolder;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>The scanner subfolders: one per account, worked out from the accounts at every run.</summary>
public sealed class AccountFoldersTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nomorepaper-folders-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _root.Delete(recursive: true);

    [Theory]
    [InlineData("anna", "anna")]
    [InlineData("Anna", "anna")]
    [InlineData("max.mustermann", "max.mustermann")]
    [InlineData("anna_b-2", "anna_b-2")]
    [InlineData("anna@example.org", null)]
    [InlineData("jürgen", null)]
    [InlineData("anna.", null)]
    [InlineData(".anna", null)]
    [InlineData("-anna", null)]
    [InlineData("..", null)]
    [InlineData("Fehler", null)]
    public void TheFolderName_IsTheLowerCaseUserName_OrElseTheId(string userName, string? expected)
    {
        var account = Account(userName);

        Assert.Equal(expected ?? account.Id.ToString("D", CultureInfo.InvariantCulture), AccountFolders.FolderNameOf(account));
    }

    [Fact]
    public async Task EveryAccount_GetsItsFolder_AndOnlyAccountFoldersAreRead()
    {
        var anna = Account("Anna");
        var bob = Account("bob");
        Directory.CreateDirectory(Path.Combine(_root.FullName, "bob"));
        foreach (var other in new[] { "carol", "Anna2", ".hidden", "Fehler" })
        {
            Directory.CreateDirectory(Path.Combine(_root.FullName, other));
        }

        var plan = await Folders(anna, bob).PrepareAsync(_root.FullName, Ct);

        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "anna")));
        Assert.Equal(
            new (string, Guid?)[] { (_root.FullName, null), (Path.Combine(_root.FullName, "anna"), anna.Id), (Path.Combine(_root.FullName, "bob"), bob.Id) },
            plan.Folders.OrderBy(folder => folder.Path, StringComparer.Ordinal));
        Assert.Equal((2, 0), (plan.UnknownFolders, plan.Failures));
    }

    [Fact]
    public async Task TheFolderOfARemovedAccount_IsUnknown_AndNeverGivenToAnybody()
    {
        var anna = Account("anna");
        await Folders(anna, Account("bob")).PrepareAsync(_root.FullName, Ct);

        // Bob is removed; his folder stays, and a new start knows nothing of him.
        var plan = await Folders(anna).PrepareAsync(_root.FullName, Ct);

        Assert.Equal(1, plan.UnknownFolders);
        Assert.DoesNotContain(plan.Folders, folder => folder.Path.EndsWith("bob", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AUserNameThatIsAnotherAccountsId_DoesNotShareItsFolder()
    {
        var odd = Account("jürgen");
        var copycat = Account(odd.Id.ToString("D", CultureInfo.InvariantCulture));

        var plan = await Folders(odd, copycat).PrepareAsync(_root.FullName, Ct);

        var owned = plan.Folders.Where(folder => folder.OwnerId is not null).ToDictionary(folder => Path.GetFileName(folder.Path), folder => folder.OwnerId);
        Assert.Equal(odd.Id, owned[odd.Id.ToString("D", CultureInfo.InvariantCulture)]);
        Assert.Equal(copycat.Id, owned[copycat.Id.ToString("D", CultureInfo.InvariantCulture)]);
    }

    private static Account Account(string userName) => new(Guid.CreateVersion7(), userName, userName, IsAdmin: false, "stamp");

    private static AccountFolders Folders(params Account[] accounts) =>
        new(new ListOnlyAccountStore(accounts), NullLogger<AccountFolders>.Instance);
}
