using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

/// <summary>First start, each on its own empty database.</summary>
public sealed class SetupTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Setup_WorksOnce_SignsTheAdministratorIn_AndShowsTheEmergencyCode()
    {
        await using var app = api.WithFreshDatabase("setup_once");
        using var client = app.CreateClient();

        Assert.True((await client.GetFromJsonAsync<SetupStatusDto>("/api/setup", Ct))!.Needed);
        using var setup = await client.PostAsJsonAsync("/api/setup", new SetupDto(" anna ", "Anna", "lange-passphrase"), Ct);

        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var code = (await setup.Content.ReadFromJsonAsync<EmergencyCodeDto>(Ct))!.EmergencyCode;
        Assert.Matches("^[A-Z2-7]{20}$", code);
        var me = await client.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct);
        Assert.Equal(("anna", "Anna", true), (me!.UserName, me.DisplayName, me.IsAdmin));
        Assert.False((await client.GetFromJsonAsync<SetupStatusDto>("/api/setup", Ct))!.Needed);

        using var again = await app.CreateClient().PostAsJsonAsync("/api/setup", new SetupDto("bob", "Bob", "lange-passphrase"), Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("application/problem+json", again.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Setup_HandsDocumentsWithoutOwner_ToTheAdministrator_Shared()
    {
        await using var app = api.WithFreshDatabase("setup_legacy");
        using var client = app.CreateClient();
        var legacy = await ApiFixture.AddLegacyDocumentAsync(app.Services);

        using var setup = await client.PostAsJsonAsync("/api/setup", new SetupDto("verwalter", "V", "lange-passphrase"), Ct);
        setup.EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{legacy}", Ct);
        Assert.Equal((true, false), (detail!.IsMine, detail.IsPrivate));
    }

    [Fact]
    public async Task Setup_RefusesShortPasswords_AndEmptyNames_PerField()
    {
        await using var app = api.WithFreshDatabase("setup_invalid");
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/setup", new SetupDto("  ", "", "kurz"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Ct);
        Assert.Equal(["invalid"], problem!.Errors!["userName"]);
        Assert.Equal(["invalid"], problem.Errors["displayName"]);
        Assert.True((await client.GetFromJsonAsync<SetupStatusDto>("/api/setup", Ct))!.Needed);

        foreach (var password in new[] { "123456789", "" })
        {
            using var shortPassword = await client.PostAsJsonAsync("/api/setup", new SetupDto("anna", "Anna", password), Ct);
            var passwordProblem = await shortPassword.Content.ReadFromJsonAsync<ProblemBody>(Ct);
            Assert.Equal(["too-short"], passwordProblem!.Errors!["password"]);
        }
    }

    [Fact]
    public async Task EmergencyReset_SetsANewPassword_AndReplacesTheCode()
    {
        await using var app = api.WithFreshDatabase("setup_emergency");
        using var setup = await app.CreateClient().PostAsJsonAsync("/api/setup", new SetupDto("chef", "Chef", "alte-passphrase"), Ct);
        var code = (await setup.Content.ReadFromJsonAsync<EmergencyCodeDto>(Ct))!.EmergencyCode;
        using var client = app.CreateClient();

        using var wrong = await client.PostAsJsonAsync("/api/emergency-reset", new EmergencyResetDto("chef", "AAAAAAAAAAAAAAAAAAAA", "neue-passphrase"), Ct);
        using var unknown = await client.PostAsJsonAsync("/api/emergency-reset", new EmergencyResetDto("niemand", code, "neue-passphrase"), Ct);
        using var tooShort = await client.PostAsJsonAsync("/api/emergency-reset", new EmergencyResetDto("chef", code, "kurz"), Ct);
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (wrong.StatusCode, unknown.StatusCode));
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        // Typed in groups and lower case, as read from paper.
        var typed = string.Join("-", code.Chunk(5).Select(group => new string(group))).ToLowerInvariant();
        using var reset = await client.PostAsJsonAsync("/api/emergency-reset", new EmergencyResetDto("chef", typed, "neue-passphrase"), Ct);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var newCode = (await reset.Content.ReadFromJsonAsync<EmergencyCodeDto>(Ct))!.EmergencyCode;
        Assert.NotEqual(code, newCode);
        using var reused = await client.PostAsJsonAsync("/api/emergency-reset", new EmergencyResetDto("chef", code, "dritte-passphrase"), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.SignInAsync("chef", "alte-passphrase", Ct)).Response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await app.SignInAsync("chef", "neue-passphrase", Ct)).Response.StatusCode);
    }
}
