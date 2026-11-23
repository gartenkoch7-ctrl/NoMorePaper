using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

/// <summary><c>/api/account/after-editing</c>: which versions the account's edited documents keep by default (M4b).</summary>
public sealed class AfterEditingApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AMember_ReadsKeepBoth_SavesAnotherChoice_AndReadsItBack()
    {
        var (member, _) = await api.CreateMemberAsync("after-editing-anna", Ct);
        Assert.Equal(VersionPolicyDto.KeepBoth, (await member.GetFromJsonAsync<AfterEditingDto>("/api/account/after-editing", Ct))!.Policy);

        using var saved = await member.PutAsJsonAsync("/api/account/after-editing", new AfterEditingDto(VersionPolicyDto.OriginalOnly), Ct);

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(VersionPolicyDto.OriginalOnly, (await member.GetFromJsonAsync<AfterEditingDto>("/api/account/after-editing", Ct))!.Policy);
        Assert.Contains("\"policy\":\"OriginalOnly\"", await member.GetStringAsync("/api/account/after-editing", Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDocumentDetail_ShowsTheOwnersDefault()
    {
        var (member, _) = await api.CreateMemberAsync("after-editing-detail", Ct);
        var id = await member.UploadPdfAsync("brief.pdf", isPrivate: false, Ct);
        using var saved = await member.PutAsJsonAsync("/api/account/after-editing", new AfterEditingDto(VersionPolicyDto.EditedOnly), Ct);

        var detail = await api.CreateAdminClient().GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);

        Assert.Equal(VersionPolicyDto.EditedOnly, detail!.VersionPolicy);
    }

    [Fact]
    public async Task AnUnknownChoice_Is400_AndWithoutSignIn_Is401()
    {
        var (member, _) = await api.CreateMemberAsync("after-editing-bad", Ct);
        using var unknown = new StringContent("""{"policy":"Everything"}""", System.Text.Encoding.UTF8, "application/json");
        using var empty = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, (await member.PutAsync("/api/account/after-editing", unknown, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PutAsync("/api/account/after-editing", empty, Ct)).StatusCode);
        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/account/after-editing", Ct)).StatusCode);
    }
}
