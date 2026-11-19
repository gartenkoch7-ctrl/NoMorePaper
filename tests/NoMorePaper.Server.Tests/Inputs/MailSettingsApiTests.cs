using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Inputs;

/// <summary><c>/api/inputs/mail</c>: administrator only, the password stored encrypted and never given back.</summary>
public sealed class MailSettingsApiTests(ApiFixture api)
{
    private const string Password = "postfach-geheim-42";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheAdministrator_SavesTheMailbox_ThePasswordStaysEncrypted_AndIsNeverSent()
    {
        using var admin = api.CreateAdminClient();
        var settings = new MailSettingsRequestDto(" imap.haushalt.example ", 993, "ssl", "dokumente", "mx.haushalt.example", null);

        using var missing = await admin.PutAsJsonAsync("/api/inputs/mail", settings, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("\"password\":[\"required\"]", await missing.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        using var saved = await admin.PutAsJsonAsync("/api/inputs/mail", settings with { Password = Password }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using var kept = await admin.PutAsJsonAsync("/api/inputs/mail", settings with { TrustedAuthServId = "mx2.haushalt.example", Password = string.Empty }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, kept.StatusCode);

        var text = await admin.GetStringAsync("/api/inputs/mail", Ct);
        var read = await admin.GetFromJsonAsync<MailSettingsDto>("/api/inputs/mail", Ct);
        Assert.Equal(new MailSettingsDto("imap.haushalt.example", 993, "ssl", "dokumente", "mx2.haushalt.example", true), read);
        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);

        await using var scope = api.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IInputConnections>().GetAsync(InputKind.Mail, null, Ct);
        Assert.NotNull(stored?.Secret);
        Assert.DoesNotContain(Password, Encoding.UTF8.GetString(stored.Secret) + stored.Settings, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("imap.example", 0, "ssl", "user")]
    [InlineData("imap.example", 993, "tls", "user")]
    [InlineData("imap example", 993, "ssl", "user")]
    [InlineData("imap.example", 993, "ssl", "")]
    public async Task UnusableSettings_Are400(string host, int port, string security, string user)
    {
        using var admin = api.CreateAdminClient();

        using var refused = await admin.PutAsJsonAsync("/api/inputs/mail", new MailSettingsRequestDto(host, port, security, user, null, Password), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("\"settings\":[\"invalid\"]", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTest_TriesThePostedSettings_AndSaysWhenTheServerIsNotReachable()
    {
        using var admin = api.CreateAdminClient();

        using var tested = await admin.PostAsJsonAsync(
            "/api/inputs/mail/test", new MailSettingsRequestDto("127.0.0.1", 1, "starttls", "dokumente", null, Password), Ct);

        Assert.Equal(HttpStatusCode.BadGateway, tested.StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1", 993, "ssl", "postmaster")]
    [InlineData("imap.other.example", 1, "ssl", "postmaster")]
    [InlineData("imap.other.example", 993, "starttls", "postmaster")]
    [InlineData("imap.other.example", 993, "ssl", "someone-else")]
    public async Task TheStoredPassword_GoesOnlyToItsServerAndUser_SaveAndTestNeedItAgainOtherwise(string host, int port, string security, string user)
    {
        using var admin = api.CreateAdminClient();
        var stored = new MailSettingsRequestDto("imap.other.example", 993, "ssl", "postmaster", null, Password);
        using var saved = await admin.PutAsJsonAsync("/api/inputs/mail", stored, Ct);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var changed = new MailSettingsRequestDto(host, port, security, user, null, null);

        using var save = await admin.PutAsJsonAsync("/api/inputs/mail", changed, Ct);
        using var test = await admin.PostAsJsonAsync("/api/inputs/mail/test", changed, Ct);

        foreach (var refused in new[] { save, test })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("\"password\":[\"required\"]", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }

        var read = await admin.GetFromJsonAsync<MailSettingsDto>("/api/inputs/mail", Ct);
        Assert.Equal(("imap.other.example", "postmaster"), (read!.Host, read.UserName));
    }

    [Fact]
    public async Task AnUnencryptedConnection_IsRefused_ForSaveAndTest_OnTheSecurityField()
    {
        using var admin = api.CreateAdminClient();
        var plain = new MailSettingsRequestDto("127.0.0.1", 1, "none", "dokumente", null, Password);

        using var save = await admin.PutAsJsonAsync("/api/inputs/mail", plain, Ct);
        using var test = await admin.PostAsJsonAsync("/api/inputs/mail/test", plain, Ct);

        foreach (var refused in new[] { save, test })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("\"security\":[\"unencrypted\"]", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AMember_GetsNothing_AndChangesNothing()
    {
        var (member, _) = await api.CreateMemberAsync("mail-member", Ct);
        var settings = new MailSettingsRequestDto("imap.example", 993, "ssl", "user", null, Password);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/inputs/mail", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync("/api/inputs/mail", settings, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/inputs/mail/test", settings, Ct)).StatusCode);
    }
}
