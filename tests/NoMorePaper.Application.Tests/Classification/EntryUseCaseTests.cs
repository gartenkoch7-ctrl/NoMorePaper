using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Domain.Classification;
using Xunit;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>Lists: names are checked before the store is asked, the store's answer becomes the outcome.</summary>
public sealed class EntryUseCaseTests
{
    private readonly FakeClassificationStore _store = new();
    private readonly FakeCurrentAccount _current = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_AsksAsTheSignedInAccount()
    {
        var entries = await new ListEntries(_store, _current).ListAsync(ClassificationKind.Tag, Ct);

        Assert.Single(entries);
        Assert.Equal((_current.Id, "list Tag Account"), (_store.LastAccount, Assert.Single(_store.Calls)));
    }

    [Fact]
    public async Task Create_TrimsTheName_AndReturnsTheEntry()
    {
        var log = new RecordingLogger<CreateEntry>();

        var created = await new CreateEntry(_store, _current, log).CreateAsync(ClassificationKind.Correspondent, "  Stadtwerke  ", Ct);

        Assert.Equal((ChangeOutcome.Changed, "Stadtwerke"), (created.Outcome, created.Entry?.Name));
        Assert.Equal((_current.Id, "find-or-create Correspondent Stadtwerke Account"), (_store.LastAccount, Assert.Single(_store.Calls)));
        Assert.Equal([5257], log.EventIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("--- ...")]
    public async Task InvalidNames_NeverReachTheStore(string? name)
    {
        var create = new CreateEntry(_store, _current, new RecordingLogger<CreateEntry>());
        var rename = new RenameEntry(_store, _current, new RecordingLogger<RenameEntry>());

        Assert.Equal(ChangeOutcome.Invalid, (await create.CreateAsync(ClassificationKind.Tag, name, Ct)).Outcome);
        Assert.Equal(ChangeOutcome.Invalid, await rename.RenameAsync(ClassificationKind.Tag, Guid.CreateVersion7(), name, Ct));
        Assert.Equal(ChangeOutcome.Invalid, (await create.CreateAsync(ClassificationKind.Tag, new string('a', ClassificationName.MaxLength + 1), Ct)).Outcome);
        Assert.Empty(_store.Calls);
    }

    [Theory]
    [InlineData(EntryChange.Changed, ChangeOutcome.Changed, 5258)]
    [InlineData(EntryChange.NotFound, ChangeOutcome.NotFound, 5260)]
    [InlineData(EntryChange.Forbidden, ChangeOutcome.Forbidden, 5260)]
    public async Task Rename_ReportsTheStoresAnswer(EntryChange change, ChangeOutcome expected, int eventId)
    {
        _store.Change = change;
        var log = new RecordingLogger<RenameEntry>();

        Assert.Equal(expected, await new RenameEntry(_store, _current, log).RenameAsync(ClassificationKind.DocumentType, Guid.CreateVersion7(), " Rechnung ", Ct));

        Assert.Equal("rename DocumentType Rechnung", Assert.Single(_store.Calls));
        Assert.Equal([eventId], log.EventIds);
    }

    [Theory]
    [InlineData(EntryChange.Changed, ChangeOutcome.Changed)]
    [InlineData(EntryChange.NotFound, ChangeOutcome.NotFound)]
    [InlineData(EntryChange.Forbidden, ChangeOutcome.Forbidden)]
    public async Task Merge_PassesDistinctSources_AndReportsTheStoresAnswer(EntryChange change, ChangeOutcome expected)
    {
        _store.Change = change;
        var source = Guid.CreateVersion7();

        Assert.Equal(expected, await Merge().MergeAsync(ClassificationKind.Tag, Guid.CreateVersion7(), [source, source], Ct));

        Assert.Equal([source], _store.LastSources);
    }

    [Fact]
    public async Task Merge_NeedsSources_OtherThanTheTarget()
    {
        var target = Guid.CreateVersion7();

        Assert.Equal(ChangeOutcome.Invalid, await Merge().MergeAsync(ClassificationKind.Tag, target, [], Ct));
        Assert.Equal(ChangeOutcome.Invalid, await Merge().MergeAsync(ClassificationKind.Tag, target, null, Ct));
        Assert.Equal(ChangeOutcome.Invalid, await Merge().MergeAsync(ClassificationKind.Tag, target, [target, Guid.CreateVersion7()], Ct));
        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task ARemovedAccount_ChangesNoList()
    {
        _current.Exists = false;

        Assert.Equal(ChangeOutcome.AccountGone, (await new CreateEntry(_store, _current, new RecordingLogger<CreateEntry>()).CreateAsync(ClassificationKind.Tag, "a", Ct)).Outcome);
        Assert.Equal(ChangeOutcome.AccountGone, await new RenameEntry(_store, _current, new RecordingLogger<RenameEntry>()).RenameAsync(ClassificationKind.Tag, Guid.CreateVersion7(), "a", Ct));
        Assert.Equal(ChangeOutcome.AccountGone, await Merge().MergeAsync(ClassificationKind.Tag, Guid.CreateVersion7(), [Guid.CreateVersion7()], Ct));
        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task Household_ListsAllMembers()
    {
        var accounts = new FakeAccountStore();
        accounts.Members.Add(new Account(Guid.CreateVersion7(), "bob", "Bob", false, "stamp"));

        Assert.Equal(2, (await new ListHousehold(accounts).ListAsync(Ct)).Count);
    }

    private MergeEntries Merge() => new(_store, _current, new RecordingLogger<MergeEntries>());
}
