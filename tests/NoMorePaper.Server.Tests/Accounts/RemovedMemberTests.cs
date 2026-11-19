using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

/// <summary>
/// A removed member's cookie stays valid until its next session check (5 minutes). In that window
/// every change asks the store again and is refused.
/// </summary>
public sealed class RemovedMemberTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARemovedMember_WithinTheCheckInterval_CanNoLongerChangeAnything()
    {
        // The fixture keeps the production interval: the cookie itself is not checked again here.
        Assert.Equal(TimeSpan.FromMinutes(5), api.Services.GetRequiredService<IOptions<AccountSessionOptions>>().Value.ValidationInterval);
        var (_, id) = await api.CreateMemberAsync("removed-uploader", Ct);
        using var signIn = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new SignInDto("removed-uploader", ApiFixtureExtensions.MemberPassword), Ct);
        var cookie = SessionCookie.From(signIn)!;
        var document = await Client(cookie).UploadPdfAsync("vor-dem-entfernen.pdf", isPrivate: false, Ct);

        using var removed = await api.CreateAdminClient().DeleteAsync($"/api/members/{id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        // A fresh client per request: a 401 that clears the cookie must not make the next one anonymous.
        using var upload = await Client(cookie).UploadAsync("nach-dem-entfernen.pdf", SampleFiles.Pdf(), "false", Ct);
        using var privacy = await Client(cookie).PatchAsJsonAsync($"/api/documents/{document}", new DocumentPrivacyDto(true), Ct);
        using var password = await Client(cookie).PostAsJsonAsync(
            "/api/account/password", new ChangePasswordDto(ApiFixtureExtensions.MemberPassword, "neues-passwort"), Ct);
        using var acknowledge = await Client(cookie).PostAsync("/api/account/acknowledge-reset", null, Ct);
        using var confirm = await Client(cookie).PostAsync($"/api/documents/{document}/confirm", null, Ct);
        using var entry = await Client(cookie).PostAsJsonAsync("/api/classification/tags", new CreateEntryDto("Nach dem Entfernen"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, privacy.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, acknowledge.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, entry.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", SessionCookie.SetCookieHeader(upload), StringComparison.OrdinalIgnoreCase);
        var shared = await api.CreateAdminClient().GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{document}", Ct);
        Assert.False(shared!.IsPrivate);
    }

    private HttpClient Client(string cookie)
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }
}
