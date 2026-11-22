using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Classification;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

/// <summary>The recognised text of a document: same visibility as the document itself, never for strangers.</summary>
public sealed class DocumentTextTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOwner_GetsTheTextWithItsLineBreaksUnchanged()
    {
        var (owner, _) = await api.CreateMemberAsync("text-owner", Ct);
        var id = await UploadAsync(owner, "text-eigentuemer.pdf", 7001, isPrivate: false);
        await api.CompleteAsync(id, "Zeile eins\nZeile zwei\r\nZeile drei");

        var text = await owner.GetFromJsonAsync<DocumentTextDto>($"/api/documents/{id}/text", Ct);

        Assert.Equal("Zeile eins\nZeile zwei\r\nZeile drei", text!.Text);
    }

    [Fact]
    public async Task ASharedDocument_GivesItsTextToAnotherMember()
    {
        var (owner, _) = await api.CreateMemberAsync("text-sharing-owner", Ct);
        var (other, _) = await api.CreateMemberAsync("text-sharing-other", Ct);
        var id = await UploadAsync(owner, "text-geteilt.pdf", 7002, isPrivate: false);
        await api.CompleteAsync(id, "Gemeinsamer Text");

        var text = await other.GetFromJsonAsync<DocumentTextDto>($"/api/documents/{id}/text", Ct);

        Assert.Equal("Gemeinsamer Text", text!.Text);
    }

    [Fact]
    public async Task APrivateDocument_GivesItsTextOnlyToItsOwner_NotEvenToTheAdministrator()
    {
        var (owner, _) = await api.CreateMemberAsync("text-private-owner", Ct);
        var (other, _) = await api.CreateMemberAsync("text-private-other", Ct);
        var id = await UploadAsync(owner, "text-privat.pdf", 7003, isPrivate: true);
        await api.CompleteAsync(id, "Geheimer Text");

        foreach (var stranger in new[] { other, api.CreateAdminClient() })
        {
            using var response = await stranger.GetAsync($"/api/documents/{id}/text", Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var own = await owner.GetFromJsonAsync<DocumentTextDto>($"/api/documents/{id}/text", Ct);
        Assert.Equal("Geheimer Text", own!.Text);
    }

    [Fact]
    public async Task ADocumentThatIsNotProcessedYet_AnswersWithoutText()
    {
        var (owner, _) = await api.CreateMemberAsync("text-queued-owner", Ct);
        var id = await UploadAsync(owner, "text-wartend.pdf", 7004, isPrivate: false);

        using var response = await owner.GetAsync($"/api/documents/{id}/text", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<DocumentTextDto>(Ct))!.Text);
    }

    [Fact]
    public async Task ADocumentWhereTheOcrFoundNothing_AnswersWithAnEmptyText()
    {
        var (owner, _) = await api.CreateMemberAsync("text-empty-owner", Ct);
        var id = await UploadAsync(owner, "text-leer.pdf", 7005, isPrivate: false);
        await api.CompleteAsync(id, string.Empty);

        var text = await owner.GetFromJsonAsync<DocumentTextDto>($"/api/documents/{id}/text", Ct);

        Assert.Equal(string.Empty, text!.Text);
    }

    [Fact]
    public async Task AnUnknownDocument_Is404()
    {
        var (member, _) = await api.CreateMemberAsync("text-unknown", Ct);

        using var response = await member.GetAsync($"/api/documents/{Guid.CreateVersion7()}/text", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithoutSignIn_TheTextIsNotServed()
    {
        using var response = await api.CreateClient().GetAsync($"/api/documents/{api.LegacyDocumentId}/text", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A length of its own per test: no other upload counts as the same file.
    private static async Task<Guid> UploadAsync(HttpClient client, string fileName, int length, bool isPrivate)
    {
        using var response = await client.UploadAsync(
            fileName, SampleFiles.WithHead("%PDF-1.7\n"u8, length), isPrivate ? "true" : "false", Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
    }
}
