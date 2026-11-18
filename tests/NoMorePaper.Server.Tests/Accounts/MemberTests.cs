using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Infrastructure.Accounts;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

public sealed class MemberTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Members_ListsEveryAccount_WithoutSecrets()
    {
        var (_, id) = await api.CreateMemberAsync("listed-member", Ct);

        using var response = await api.CreateAdminClient().GetAsync("/api/members", Ct);
        var json = await response.Content.ReadAsStringAsync(Ct);
        var members = await response.Content.ReadFromJsonAsync<AccountDto[]>(Ct);

        Assert.Contains(members!, member => member.Id == id && member.UserName == "listed-member" && !member.IsAdmin);
        Assert.Contains(members!, member => member.UserName == ApiFixture.AdminUserName && member.IsAdmin);
        Assert.DoesNotContain("stamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Remove_RefusesTheAdministratorItself_AndUnknownIds()
    {
        using var admin = api.CreateAdminClient();
        var adminId = (await admin.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.Id;

        using var self = await admin.DeleteAsync($"/api/members/{adminId}", Ct);
        using var unknown = await admin.DeleteAsync($"/api/members/{Guid.CreateVersion7()}", Ct);

        Assert.Equal(["self"], (await self.Content.ReadFromJsonAsync<ProblemBody>(Ct))!.Errors!["id"]);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/auth/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task ResetLink_SetsANewPassword_Once_WithinADay()
    {
        var (_, id) = await api.CreateMemberAsync("forgetful-member", Ct);
        using var created = await api.CreateAdminClient().PostAsync($"/api/members/{id}/reset-link", null, Ct);
        var link = (await created.Content.ReadFromJsonAsync<LinkDto>(Ct))!;
        var token = ApiFixtureExtensions.TokenOf(link.Link);
        using var client = api.CreateClient();

        Assert.StartsWith("http://localhost/passwort/", link.Link, StringComparison.Ordinal);
        Assert.InRange(link.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromHours(23.9), TimeSpan.FromHours(24));
        using var tooShort = await client.PostAsJsonAsync($"/api/password-reset/{token}", new NewPasswordDto("kurz"), Ct);
        using var reset = await client.PostAsJsonAsync($"/api/password-reset/{token}", new NewPasswordDto("neues-passwort"), Ct);
        using var again = await client.PostAsJsonAsync($"/api/password-reset/{token}", new NewPasswordDto("drittes-passwort"), Ct);

        Assert.Equal(["too-short"], (await tooShort.Content.ReadFromJsonAsync<ProblemBody>(Ct))!.Errors!["newPassword"]);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.SignInAsync("forgetful-member", ApiFixtureExtensions.MemberPassword, Ct)).Response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.SignInAsync("forgetful-member", "neues-passwort", Ct)).Response.StatusCode);
    }

    [Fact]
    public async Task ResetLink_Expired_OrForAnUnknownMember_IsRefused()
    {
        const string token = "expired-reset-token-for-the-test-only";
        var (_, id) = await api.CreateMemberAsync("expired-reset-member", Ct);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                TokenHash = SecretHash.Of(token),
                UserId = id,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync(Ct);
        }

        using var expired = await api.CreateClient().PostAsJsonAsync($"/api/password-reset/{token}", new NewPasswordDto("neues-passwort"), Ct);
        using var unknown = await api.CreateAdminClient().PostAsync($"/api/members/{Guid.CreateVersion7()}/reset-link", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
