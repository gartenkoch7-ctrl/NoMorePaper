using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Tests.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs;

/// <summary>Who sees which input states on the page "Inputs".</summary>
public sealed class ListInputStatusTests
{
    private static readonly Account s_anna = new(Guid.CreateVersion7(), "anna", "Anna", false, "stamp");
    private static readonly Account s_ben = new(Guid.CreateVersion7(), "ben", "Ben", false, "stamp");

    private readonly FakeAccountStore _accounts = new();
    private readonly FakeStatusStore _status = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ListInputStatusTests()
    {
        _accounts.Members.Clear();
        _accounts.Members.AddRange([s_anna, s_ben]);
        _status.Rows.AddRange(
        [
            Row(InputKind.ScannerFolder, null),
            Row(InputKind.Mail, null),
            Row(InputKind.ICloud, s_anna.Id),
            Row(InputKind.ICloud, s_ben.Id),
        ]);
    }

    [Fact]
    public async Task TheAdministrator_SeesEveryRow_WithTheAccountsNames()
    {
        var rows = await List(new FakeCurrentAccount { IsAdmin = true }).ListAsync(Ct);

        Assert.Equal([null, null, "Anna", "Ben"], rows.Select(row => row.AccountName));
    }

    [Fact]
    public async Task AMember_SeesScannerAndMail_AndOnlyItsOwnICloud()
    {
        var rows = await List(new FakeCurrentAccount { Id = s_anna.Id }).ListAsync(Ct);

        Assert.Equal(
            [(InputKind.ScannerFolder, null), (InputKind.Mail, null), (InputKind.ICloud, "Anna")],
            rows.Select(row => (row.Status.Kind, row.AccountName)));
    }

    [Fact]
    public async Task WithoutAccountRows_NoNamesAreLoaded()
    {
        _status.Rows.RemoveAll(row => row.AccountId is not null);
        _accounts.Calls.Clear();

        var rows = await List(new FakeCurrentAccount { IsAdmin = true }).ListAsync(Ct);

        Assert.Equal(2, rows.Count);
        Assert.Empty(_accounts.Calls);
    }

    [Fact]
    public async Task ARowOfARemovedAccount_HasNoName()
    {
        _accounts.Members.Remove(s_ben);

        var rows = await List(new FakeCurrentAccount { IsAdmin = true }).ListAsync(Ct);

        Assert.Null(rows[^1].AccountName);
    }

    private ListInputStatus List(FakeCurrentAccount current) => new(_status, _accounts, current);

    private static InputStatus Row(InputKind kind, Guid? accountId) =>
        new(kind, accountId, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, false, 0);

    private sealed class FakeStatusStore : IInputStatusStore
    {
        public List<InputStatus> Rows { get; } = [];

        public Task RecordAsync(InputKind kind, Guid? accountId, InputRunResult result, DateTimeOffset at, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<InputStatus>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<InputStatus>>([.. Rows]);
    }
}
