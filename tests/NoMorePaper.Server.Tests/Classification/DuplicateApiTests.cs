using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Classification;

/// <summary>
/// Exact duplicates at upload (409 with the existing document, but only among documents the uploader
/// may see), possible duplicates in "to review", keeping and deleting them.
/// </summary>
public sealed class DuplicateApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSameFileAgain_IsRefusedWith409_NamingTheVisibleOriginal()
    {
        var (anna, _) = await api.CreateMemberAsync("dup-anna", Ct);
        var (bob, _) = await api.CreateMemberAsync("dup-bob", Ct);
        var shared = SampleFiles.Pdf();
        var secret = SampleFiles.Pdf();
        using var first = await anna.UploadAsync("brief.pdf", shared, "false", Ct);
        using var hidden = await anna.UploadAsync("geheim.pdf", secret, "true", Ct);
        var original = (await first.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;

        using var again = await anna.UploadAsync("brief-nochmal.pdf", shared, "true", Ct);
        using var bobsCopy = await bob.UploadAsync("brief.pdf", shared, "false", Ct);
        using var bobsSecret = await bob.UploadAsync("geheim.pdf", secret, "false", Ct);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, bobsCopy.StatusCode);
        using var problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync(Ct));
        Assert.Equal(original, problem.RootElement.GetProperty("existingId").GetGuid());

        // Bob may not see Anna's private copy, so his upload is accepted: a 409 would reveal it.
        Assert.Equal(HttpStatusCode.Accepted, bobsSecret.StatusCode);
    }

    [Fact]
    public async Task Review_ListsWhatTheAccountMayEdit_AndHidesInvisibleDuplicates()
    {
        var (owner, _) = await api.CreateMemberAsync("review-owner", Ct);
        var (other, _) = await api.CreateMemberAsync("review-other", Ct);
        var original = await owner.UploadPdfAsync("original.pdf", isPrivate: false, Ct);
        var automatic = await owner.UploadPdfAsync("erkannt.pdf", isPrivate: false, Ct);
        var copy = await owner.UploadPdfAsync("kopie.pdf", isPrivate: false, Ct);
        var othersSecret = await other.UploadPdfAsync("geheim.pdf", isPrivate: true, Ct);
        var hiddenCopy = await owner.UploadPdfAsync("versteckt.pdf", isPrivate: false, Ct);
        await api.ClassifyAsync(automatic, Recognition.Fields());
        await api.MarkDuplicateAsync(copy, original);
        await api.MarkDuplicateAsync(hiddenCopy, othersSecret);

        var mine = await owner.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct);
        var theirs = await other.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct);

        Assert.NotNull(mine);
        Assert.Equal([copy, automatic], mine.Select(item => item.Id));
        Assert.Equal(original, mine[0].PossibleDuplicate?.Id);
        Assert.Equal("Abschlag für November.", mine[1].Fields.Summary);
        Assert.DoesNotContain(theirs!, item => item.Id == automatic || item.Id == copy);
        Assert.Null((await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{hiddenCopy}/fields", Ct))!.PossibleDuplicateOf);

        // The number for the navigation follows the same rule.
        Assert.Equal(mine.Length, await owner.GetFromJsonAsync<int>("/api/review/count", Ct));
        Assert.Equal(theirs!.Length, await other.GetFromJsonAsync<int>("/api/review/count", Ct));
    }

    [Fact]
    public async Task KeepAndDelete_ResolveAPossibleDuplicate()
    {
        var (owner, _) = await api.CreateMemberAsync("dup-owner", Ct);
        var (editor, editorId) = await api.CreateMemberAsync("dup-editor", Ct);
        var original = await owner.UploadPdfAsync("vertrag.pdf", isPrivate: false, Ct);
        var kept = await owner.UploadPdfAsync("vertrag-2.pdf", isPrivate: false, Ct);
        var deleted = await owner.UploadPdfAsync("vertrag-3.pdf", isPrivate: false, Ct);
        await api.MarkDuplicateAsync(kept, original);
        await api.MarkDuplicateAsync(deleted, original);
        using var editors = await owner.PutAsJsonAsync($"/api/documents/{deleted}/editors", new SetEditorsRequestDto([editorId]), Ct);
        Assert.Equal(HttpStatusCode.NoContent, editors.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/documents/{kept}/duplicate/keep", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/documents/{kept}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync($"/api/documents/{deleted}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/documents/{deleted}", Ct)).StatusCode);

        Assert.Null((await owner.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{kept}/fields", Ct))!.PossibleDuplicateOf);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/documents/{deleted}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/documents/{deleted}/files/original", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/documents/{original}", Ct)).StatusCode);
    }
}
