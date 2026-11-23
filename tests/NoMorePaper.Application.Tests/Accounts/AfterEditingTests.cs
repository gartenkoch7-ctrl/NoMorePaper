using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>The account's default which versions an edited document keeps (ADR-0025).</summary>
public sealed class AfterEditingTests
{
    private readonly FakeAccountPreferencesStore _preferences = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<ChangeAfterEditing> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_IsKeepBoth_UntilTheAccountChoosesOtherwise()
    {
        var get = new GetAfterEditing(_preferences, _current);
        Assert.Equal(VersionPolicy.KeepBoth, await get.GetAsync(Ct));

        Assert.Equal(PreferencesOutcome.Saved, await Change().ChangeAsync(VersionPolicy.OriginalOnly, Ct));

        Assert.Equal(VersionPolicy.OriginalOnly, await get.GetAsync(Ct));
        Assert.Equal([5114], _log.EventIds);
    }

    [Fact]
    public async Task Change_RefusesUnknownChoices()
    {
        Assert.Equal(PreferencesOutcome.Invalid, await Change().ChangeAsync((VersionPolicy)9, Ct));
        Assert.Empty(_preferences.AfterEditing);
    }

    [Fact]
    public async Task Change_ByARemovedAccount_SavesNothing()
    {
        _current.Exists = false;
        Assert.Equal(PreferencesOutcome.AccountGone, await Change().ChangeAsync(VersionPolicy.EditedOnly, Ct));

        _current.Exists = true;
        _preferences.RemovedWhileSaving = true;
        Assert.Equal(PreferencesOutcome.AccountGone, await Change().ChangeAsync(VersionPolicy.EditedOnly, Ct));

        Assert.Empty(_preferences.AfterEditing);
        Assert.Equal([5113, 5113], _log.EventIds);
    }

    private ChangeAfterEditing Change() => new(_preferences, _current, _log);
}
