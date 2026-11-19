using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Inputs;

/// <summary><c>/api/account/preferences</c>: own preferences only; an address belongs to one account.</summary>
public sealed class PreferencesApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AMember_ReadsDefaults_SavesAndReadsBack()
    {
        var (member, _) = await api.CreateMemberAsync("prefs-anna", Ct);

        var defaults = await member.GetFromJsonAsync<AccountPreferencesDto>("/api/account/preferences", Ct);
        Assert.Equal((false, 0), (defaults!.NewDocumentsPrivate, defaults.MailSenders.Count));

        using var saved = await member.PutAsJsonAsync("/api/account/preferences", new AccountPreferencesDto(true, [" Anna@Example.ORG "]), Ct);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        var read = await member.GetFromJsonAsync<AccountPreferencesDto>("/api/account/preferences", Ct);
        Assert.True(read!.NewDocumentsPrivate);
        Assert.Equal(["anna@example.org"], read.MailSenders);
    }

    [Fact]
    public async Task AnInvalidAddress_Is400_AndAnotherAccountsAddress_Is409_OnTheSendersField()
    {
        var (first, _) = await api.CreateMemberAsync("prefs-first", Ct);
        var (second, _) = await api.CreateMemberAsync("prefs-second", Ct);
        using var taken = await first.PutAsJsonAsync("/api/account/preferences", new AccountPreferencesDto(false, ["shared@family.example"]), Ct);
        Assert.Equal(HttpStatusCode.NoContent, taken.StatusCode);

        using var invalid = await second.PutAsJsonAsync("/api/account/preferences", new AccountPreferencesDto(false, ["kein-adresse"]), Ct);
        using var conflict = await second.PutAsJsonAsync("/api/account/preferences", new AccountPreferencesDto(false, ["shared@family.example"]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("\"mailSenders\":[\"invalid\"]", await invalid.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("\"mailSenders\":[\"taken\"]", await conflict.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutSignIn_Is401()
    {
        using var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/account/preferences", Ct)).StatusCode);
    }
}
