using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Preferences of an account: address rules, and that an address belongs to one account only.</summary>
public sealed class PreferencesTests
{
    private readonly FakeAccountPreferencesStore _store = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<ChangePreferences> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(" Anna@Example.ORG ", "anna@example.org")]
    [InlineData("a.b+c@mail.example.de", "a.b+c@mail.example.de")]
    public void NormalizeAddress_TrimsAndLowers(string entered, string expected) =>
        Assert.Equal(expected, AccountPreferences.NormalizeAddress(entered));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anna")]
    [InlineData("@example.org")]
    [InlineData("anna@")]
    [InlineData("anna@example")]
    [InlineData("anna@@example.org")]
    [InlineData("an na@example.org")]
    [InlineData("anna@example..org")]
    [InlineData("anna@example.")]
    [InlineData("anna@.org")]
    public void NormalizeAddress_RefusesWhatIsNoAddress(string? entered) =>
        Assert.Null(AccountPreferences.NormalizeAddress(entered));

    [Fact]
    public void NormalizeAddress_RefusesTooLongAddresses() =>
        Assert.Null(AccountPreferences.NormalizeAddress(new string('a', 315) + "@b.example"));

    [Fact]
    public void Create_DropsDuplicates_AndLimitsTheCount()
    {
        var preferences = AccountPreferences.Create(true, ["A@b.example", "a@b.example "], out var invalid);
        Assert.True(preferences!.NewDocumentsPrivate);
        Assert.Equal(["a@b.example"], preferences.MailSenders);
        Assert.Null(invalid);

        var many = Enumerable.Range(0, AccountPreferences.MaxMailSenders + 1).Select(i => $"a{i}@b.example");
        Assert.Null(AccountPreferences.Create(false, many, out _));
    }

    [Fact]
    public void Default_IsSharedWithoutSenders() =>
        Assert.Equal((false, 0), (AccountPreferences.Default.NewDocumentsPrivate, AccountPreferences.Default.MailSenders.Count));

    [Fact]
    public async Task Change_SavesForTheSignedInAccount()
    {
        var result = await Change().ChangeAsync(true, ["Anna@Example.org"], Ct);

        Assert.Equal(new PreferencesResult(PreferencesOutcome.Saved), result);
        Assert.Equal(["anna@example.org"], _store.Saved[_current.Id].MailSenders);
        Assert.Equal(_current.Id, await _store.FindOwnerOfSenderAsync("ANNA@example.org", Ct));
        Assert.Equal(AccountPreferences.Default, await new GetPreferences(_store, new FakeCurrentAccount()).GetAsync(Ct));
    }

    [Fact]
    public async Task Change_RefusesAnInvalidAddress_AndSavesNothing()
    {
        var result = await Change().ChangeAsync(false, ["ok@b.example", "kaputt"], Ct);

        Assert.Equal(new PreferencesResult(PreferencesOutcome.Invalid, "kaputt"), result);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Change_WithoutSenderList_IsInvalid_SoNoAddressIsLostByAccident()
    {
        _store.Saved[_current.Id] = new AccountPreferences(false, ["a@b.example"]);

        Assert.Equal(new PreferencesResult(PreferencesOutcome.Invalid), await Change().ChangeAsync(true, null, Ct));
        Assert.Equal(["a@b.example"], _store.Saved[_current.Id].MailSenders);
    }

    [Fact]
    public async Task Change_RefusesAnAddressOfAnotherAccount()
    {
        _store.Saved[Guid.CreateVersion7()] = new AccountPreferences(false, ["papa@b.example"]);

        var result = await Change().ChangeAsync(false, ["papa@b.example"], Ct);

        Assert.Equal(new PreferencesResult(PreferencesOutcome.SenderTaken, "papa@b.example"), result);
        Assert.False(_store.Saved.ContainsKey(_current.Id));
    }

    [Fact]
    public async Task Change_ByARemovedAccount_ChangesNothing()
    {
        _current.Exists = false;

        Assert.Equal(PreferencesOutcome.AccountGone, (await Change().ChangeAsync(true, null, Ct)).Outcome);
        Assert.Empty(_store.Saved);
        Assert.Single(_log.EventIds);
    }

    private ChangePreferences Change() => new(_store, _current, _log);
}
