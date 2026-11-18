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

public sealed class InvitationTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invitation_IsALinkToTheAppPage_ValidForSevenDays_AndWorksOnce()
    {
        using var admin = api.CreateAdminClient();
        using var created = await admin.PostAsync("/api/invitations", null, Ct);
        var link = (await created.Content.ReadFromJsonAsync<LinkDto>(Ct))!;
        var token = ApiFixtureExtensions.TokenOf(link.Link);

        Assert.StartsWith("http://localhost/einladung/", link.Link, StringComparison.Ordinal);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.InRange(link.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7));
        var open = await api.CreateClient().GetFromJsonAsync<InvitationDto>($"/api/invitations/{token}", Ct);
        Assert.Equal(link.ExpiresAt, open!.ExpiresAt);

        using var member = api.CreateClient();
        using var accepted = await Accept(member, token, "invited-member");
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var me = await member.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct);
        Assert.Equal(("invited-member", false), (me!.UserName, me.IsAdmin));

        using var checkAgain = await api.CreateClient().GetAsync($"/api/invitations/{token}", Ct);
        using var acceptAgain = await Accept(api.CreateClient(), token, "second-member");
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (checkAgain.StatusCode, acceptAgain.StatusCode));
    }

    [Fact]
    public async Task Invitation_RefusedInput_LeavesTheLinkUsable()
    {
        await api.CreateMemberAsync("taken-name", Ct);
        using var admin = api.CreateAdminClient();
        var token = await admin.InviteAsync(Ct);

        using var taken = await Accept(api.CreateClient(), token, "TAKEN-NAME");
        using var accepted = await Accept(api.CreateClient(), token, "free-name");

        Assert.Equal(["taken"], (await taken.Content.ReadFromJsonAsync<ProblemBody>(Ct))!.Errors!["userName"]);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task Invitation_Expired_OrUnknown_Is404()
    {
        const string token = "expired-invitation-token-for-the-test-only";
        var adminId = (await api.CreateAdminClient().GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.Id;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Invitations.Add(new Invitation
            {
                Id = Guid.CreateVersion7(),
                TokenHash = SecretHash.Of(token),
                CreatedBy = adminId,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-8),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync(Ct);
        }

        using var expired = await api.CreateClient().GetAsync($"/api/invitations/{token}", Ct);
        using var expiredAccept = await Accept(api.CreateClient(), token, "late-member");
        using var unknown = await api.CreateClient().GetAsync("/api/invitations/no-such-token", Ct);

        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expiredAccept.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task MembersOtherThanTheAdministrator_CannotManageTheHousehold()
    {
        var (member, _) = await api.CreateMemberAsync("plain-member", Ct);
        var adminId = (await api.CreateAdminClient().GetFromJsonAsync<AccountDto>("/api/auth/me", Ct))!.Id;

        HttpResponseMessage[] responses =
        [
            await member.PostAsync("/api/invitations", null, Ct),
            await member.GetAsync("/api/members", Ct),
            await member.DeleteAsync($"/api/members/{adminId}", Ct),
            await member.PostAsync($"/api/members/{adminId}/reset-link", null, Ct),
        ];

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        Assert.Equal(HttpStatusCode.OK, (await api.CreateAdminClient().GetAsync("/api/auth/me", Ct)).StatusCode);
    }

    private static Task<HttpResponseMessage> Accept(HttpClient client, string token, string userName) =>
        client.PostAsJsonAsync(
            $"/api/invitations/{token}/accept", new AcceptInvitationDto(userName, "Neu", ApiFixtureExtensions.MemberPassword), Ct);
}
