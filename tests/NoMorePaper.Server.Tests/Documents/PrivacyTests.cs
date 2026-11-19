using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

/// <summary>A private document exists only for its owner: elsewhere it is 404, never 403, and absent from lists.</summary>
public sealed class PrivacyTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APrivateDocument_IsInvisibleToEverybodyElse_EvenTheAdministrator()
    {
        var (owner, _) = await api.CreateMemberAsync("private-owner", Ct);
        var (other, _) = await api.CreateMemberAsync("private-other", Ct);
        var id = await owner.UploadPdfAsync("geheim-kontoauszug.pdf", isPrivate: true, Ct);
        await CompleteAsync(id, "Der geheime Kontoauszug Zebrafink.");

        foreach (var stranger in new[] { other, api.CreateAdminClient() })
        {
            var list = await stranger.GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents?pageSize=100", Ct);
            var hits = await stranger.GetFromJsonAsync<SearchHitDto[]>("/api/search?q=Zebrafink", Ct);
            using var detail = await stranger.GetAsync($"/api/documents/{id}", Ct);
            using var file = await stranger.GetAsync($"/api/documents/{id}/files/original", Ct);
            using var patch = await stranger.PatchAsJsonAsync($"/api/documents/{id}", new DocumentPrivacyDto(false), Ct);

            Assert.DoesNotContain(list!, document => document.Id == id);
            Assert.Empty(hits!);
            Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, file.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        }

        var own = await owner.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);
        var ownHit = Assert.Single((await owner.GetFromJsonAsync<SearchHitDto[]>("/api/search?q=Zebrafink", Ct))!);
        Assert.Equal((true, true), (own!.IsPrivate, own.IsMine));
        Assert.Equal((id, true, true), (ownHit.Id, ownHit.IsPrivate, ownHit.IsMine));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/documents/{id}/files/original", Ct)).StatusCode);
    }

    [Fact]
    public async Task AnUploadWithoutPrivateField_FollowsTheAccountsSetting_AnExplicitChoiceWins_AndABadValueIs400()
    {
        var (member, _) = await api.CreateMemberAsync("upload-private-by-account", Ct);
        using var preference = await member.PutAsJsonAsync("/api/account/preferences", new AccountPreferencesDto(true, []), Ct);
        Assert.Equal(HttpStatusCode.NoContent, preference.StatusCode);

        // Lengths of their own: no other test's upload counts as the same file.
        using var unchosen = await member.UploadAsync("ohne-wahl.pdf", SampleFiles.WithHead("%PDF-1.7\n"u8, 6011), null, Ct);
        using var shared = await member.UploadAsync("geteilt.pdf", SampleFiles.WithHead("%PDF-1.7\n"u8, 6012), "false", Ct);
        using var malformed = await member.UploadAsync("kaputt.pdf", SampleFiles.WithHead("%PDF-1.7\n"u8, 6013), "yes", Ct);

        var (byAccount, appliedByAccount) = (await unchosen.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!;
        var (chosen, appliedChoice) = (await shared.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!;
        Assert.Equal((true, false), (appliedByAccount, appliedChoice));
        Assert.True((await member.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{byAccount}", Ct))!.IsPrivate);
        Assert.False((await member.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{chosen}", Ct))!.IsPrivate);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task ASharedDocument_IsVisibleToTheHousehold_ButOnlyItsOwnerMayHideIt()
    {
        var (owner, _) = await api.CreateMemberAsync("sharing-owner", Ct);
        var (other, _) = await api.CreateMemberAsync("sharing-other", Ct);
        var id = await owner.UploadPdfAsync("gemeinsam.pdf", isPrivate: false, Ct);

        var seen = await other.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);
        Assert.Equal((false, false), (seen!.IsPrivate, seen.IsMine));
        Assert.Contains((await other.GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents?pageSize=100", Ct))!, d => d.Id == id && !d.IsMine);
        using var refused = await other.PatchAsJsonAsync($"/api/documents/{id}", new DocumentPrivacyDto(true), Ct);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var hidden = await owner.PatchAsJsonAsync($"/api/documents/{id}", new DocumentPrivacyDto(true), Ct);
        Assert.Equal(HttpStatusCode.NoContent, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/documents/{id}", Ct)).StatusCode);

        using var shared = await owner.PatchAsJsonAsync($"/api/documents/{id}", new DocumentPrivacyDto(false), Ct);
        Assert.Equal(HttpStatusCode.NoContent, shared.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/documents/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Privacy_MustBeStatedExactly()
    {
        using var admin = api.CreateAdminClient();
        var id = await admin.UploadPdfAsync("eindeutig.pdf", isPrivate: false, Ct);

        using var upload = await admin.UploadAsync("vielleicht.pdf", SampleFiles.Pdf(), "maybe", Ct);
        using var missing = await admin.PatchAsync($"/api/documents/{id}", JsonContent.Create(new { }), Ct);
        using var unknown = await admin.PatchAsJsonAsync($"/api/documents/{Guid.CreateVersion7()}", new DocumentPrivacyDto(true), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.False((await admin.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct))!.IsPrivate);
    }

    [Fact]
    public async Task UploadsBelongToTheUploader()
    {
        using var admin = api.CreateAdminClient();
        var id = await admin.UploadPdfAsync("meins.pdf", isPrivate: false, Ct);

        var detail = await admin.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);
        var legacy = await admin.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{api.LegacyDocumentId}", Ct);

        Assert.Equal((true, false), (detail!.IsMine, detail.IsPrivate));
        Assert.Equal((true, false), (legacy!.IsMine, legacy.IsPrivate));
    }

    private async Task CompleteAsync(Guid id, string text)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.Documents.FindAsync([id], Ct);
        document!.StartProcessing();
        document.Complete(text);
        await db.SaveChangesAsync(Ct);
    }
}
