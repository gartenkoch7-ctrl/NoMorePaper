using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

/// <summary>Ending sessions on other devices: the security stamp, checked on every request here.</summary>
public sealed class SessionTests(ApiFixture api)
{
    private const string Password = ApiFixtureExtensions.MemberPassword;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Sessions_AreCheckedEveryFiveMinutes_InProduction()
    {
        var options = api.Services.GetRequiredService<IOptions<AccountSessionOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(5), options.ValidationInterval);
    }

    [Fact]
    public async Task SignOutEverywhere_EndsEverySessionOfTheAccount()
    {
        await api.CreateMemberAsync("everywhere-member", Ct);
        await using var app = api.WithSessionCheckOnEveryRequest();
        var (phone, _) = await app.SignInAsync("everywhere-member", Password, Ct);
        var (laptop, _) = await app.SignInAsync("everywhere-member", Password, Ct);
        var (other, _) = await app.SignInAsync(ApiFixture.AdminUserName, ApiFixture.AdminPassword, Ct);

        using var signOut = await phone.PostAsync("/api/auth/logout-everywhere", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/documents", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await app.SignInAsync("everywhere-member", Password, Ct)).Response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_KeepsThisSession_AndEndsTheOthers()
    {
        await api.CreateMemberAsync("change-member", Ct);
        await using var app = api.WithSessionCheckOnEveryRequest();
        var (current, _) = await app.SignInAsync("change-member", Password, Ct);
        var (other, _) = await app.SignInAsync("change-member", Password, Ct);

        using var wrong = await current.PostAsJsonAsync("/api/account/password", new ChangePasswordDto("falsch-falsch", "neues-passwort"), Ct);
        using var tooShort = await current.PostAsJsonAsync("/api/account/password", new ChangePasswordDto(Password, "kurz"), Ct);
        using var changed = await current.PostAsJsonAsync("/api/account/password", new ChangePasswordDto(Password, "neues-passwort"), Ct);

        Assert.Equal(["wrong"], (await wrong.Content.ReadFromJsonAsync<ProblemBody>(Ct))!.Errors!["currentPassword"]);
        Assert.Equal(["too-short"], (await tooShort.Content.ReadFromJsonAsync<ProblemBody>(Ct))!.Errors!["newPassword"]);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.SignInAsync("change-member", Password, Ct)).Response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await app.SignInAsync("change-member", "neues-passwort", Ct)).Response.StatusCode);
    }

    [Fact]
    public async Task RemovingAMember_EndsItsSessions_AndItCannotSignInAgain()
    {
        var (_, id) = await api.CreateMemberAsync("removed-member", Ct);
        await using var app = api.WithSessionCheckOnEveryRequest();
        var (member, _) = await app.SignInAsync("removed-member", Password, Ct);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/documents", Ct)).StatusCode);

        using var removed = await api.CreateAdminClient().DeleteAsync($"/api/members/{id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/documents", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.SignInAsync("removed-member", Password, Ct)).Response.StatusCode);
    }

    [Fact]
    public async Task ATamperedCookie_IsNotASession()
    {
        var (member, _) = await api.CreateMemberAsync("tamper-member", Ct);
        using var forged = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        forged.DefaultRequestHeaders.Add("Cookie", SessionCookie.Name + "=CfDJ8AAAAforged");

        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/auth/me", Ct)).StatusCode);
    }
}
