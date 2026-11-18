using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

/// <summary>
/// A member whose password was set through the administrator's reset link is told so at the next
/// sign-in (<see cref="AccountDto.AdminResetAt"/>) until it confirms the notice.
/// </summary>
public sealed class AdminResetNoticeTests(ApiFixture api)
{
    private const string NewPassword = "reset-password-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResetLink_IsReported_ToTheMemberOnly_UntilAcknowledged()
    {
        var (_, id) = await api.CreateMemberAsync("notice-member", Ct);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await ResetThroughLinkAsync(id);
        var (member, signIn) = await api.SignInAsync("notice-member", NewPassword, Ct);
        using var client = member;
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var reported = (await client.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.AdminResetAt;
        var listed = await api.CreateAdminClient().GetFromJsonAsync<AccountDto[]>("/api/members", Ct);
        using var acknowledged = await client.PostAsync("/api/account/acknowledge-reset", null, Ct);
        var afterwards = (await client.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.AdminResetAt;
        using var again = await client.PostAsync("/api/account/acknowledge-reset", null, Ct);

        Assert.NotNull(reported);
        Assert.InRange(reported.Value, before, DateTimeOffset.UtcNow);
        Assert.Null(listed!.Single(account => account.Id == id).AdminResetAt);
        Assert.Equal(HttpStatusCode.NoContent, acknowledged.StatusCode);
        Assert.Null(afterwards);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task OwnPasswordChange_IsNotReported()
    {
        var (member, _) = await api.CreateMemberAsync("own-change-member", Ct);
        using var client = member;

        using var changed = await client.PostAsJsonAsync(
            "/api/account/password", new ChangePasswordDto(ApiFixtureExtensions.MemberPassword, NewPassword), Ct);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Null((await client.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.AdminResetAt);
    }

    [Fact]
    public async Task EmergencyReset_IsNotReported()
    {
        await using var app = api.WithFreshDatabase("notice_emergency");
        using var setup = await app.CreateClient().PostAsJsonAsync("/api/setup", new SetupDto("chef", "Chef", "alte-passphrase"), Ct);
        var code = (await setup.Content.ReadFromJsonAsync<EmergencyCodeDto>(Ct))!.EmergencyCode;

        using var reset = await app.CreateClient().PostAsJsonAsync(
            "/api/emergency-reset", new EmergencyResetDto("chef", code, "neue-passphrase"), Ct);
        var (admin, signIn) = await app.SignInAsync("chef", "neue-passphrase", Ct);
        using var client = admin;

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        Assert.Null((await client.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.AdminResetAt);
    }

    [Fact]
    public async Task Acknowledge_NeedsSignIn()
    {
        using var response = await api.CreateClient().PostAsync("/api/account/acknowledge-reset", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task ResetThroughLinkAsync(Guid memberId)
    {
        using var admin = api.CreateAdminClient();
        using var created = await admin.PostAsync($"/api/members/{memberId}/reset-link", null, Ct);
        var link = (await created.Content.ReadFromJsonAsync<LinkDto>(Ct))!;
        using var reset = await api.CreateClient().PostAsJsonAsync(
            $"/api/password-reset/{ApiFixtureExtensions.TokenOf(link.Link)}", new NewPasswordDto(NewPassword), Ct);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }
}
