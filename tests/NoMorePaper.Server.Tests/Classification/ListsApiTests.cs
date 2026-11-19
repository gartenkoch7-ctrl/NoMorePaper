using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Classification;

/// <summary>
/// <c>/api/classification/{kind}</c>: entries of private documents stay private, and renaming or merging
/// needs the right to edit every affected document.
/// </summary>
public sealed class ListsApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APrivateEntry_IsNotInAnotherMembersList_UntilASharedDocumentUsesIt()
    {
        var (anna, _) = await api.CreateMemberAsync("lists-anna", Ct);
        var (bob, _) = await api.CreateMemberAsync("lists-bob", Ct);
        var secret = await anna.CreateEntryAsync("correspondents", "Psychiatrie Lindenhof", Ct);
        var document = await anna.UploadPdfAsync("befund.pdf", isPrivate: true, Ct);
        await api.ClassifyAsync(document, Recognition.Fields(correspondentId: secret));

        Assert.DoesNotContain((await bob.GetFromJsonAsync<EntryDto[]>("/api/classification/correspondents", Ct))!, entry => entry.Id == secret);
        Assert.Contains((await anna.GetFromJsonAsync<EntryDto[]>("/api/classification/correspondents", Ct))!, entry => entry.Id == secret);

        // Bob asks for the same name: he gets an entry of his own, never Anna's.
        var bobs = await bob.CreateEntryAsync("correspondents", "psychiatrie-lindenhof", Ct);
        Assert.NotEqual(secret, bobs);
        Assert.Equal(secret, await anna.CreateEntryAsync("correspondents", "Psychiatrie Lindenhof GmbH", Ct));
    }

    [Fact]
    public async Task UnknownLists_AndInvalidNames_AreRefused()
    {
        using var admin = api.CreateAdminClient();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/classification/senders", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/classification/senders", new CreateEntryDto("x"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/classification/tags", new CreateEntryDto("  "), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/classification/tags", new CreateEntryDto(new string('a', 201)), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/classification/document-types", Ct)).StatusCode);
    }

    [Fact]
    public async Task Rename_NeedsTheRightToEditEveryDocument_AndHiddenEntriesAreNotFound()
    {
        var (anna, _) = await api.CreateMemberAsync("rename-anna", Ct);
        var (bob, _) = await api.CreateMemberAsync("rename-bob", Ct);
        var tag = await anna.CreateEntryAsync("tags", "Stromm", Ct);
        var hidden = await anna.CreateEntryAsync("tags", "Nur Anna", Ct);
        var annas = await anna.UploadPdfAsync("strom.pdf", isPrivate: false, Ct);
        await api.ClassifyAsync(annas, Recognition.Fields(tagIds: tag));

        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/classification/tags/{tag}", new RenameEntryDto("Strom"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/classification/tags/{hidden}", new RenameEntryDto("x"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PutAsJsonAsync($"/api/classification/tags/{tag}", new RenameEntryDto(""), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anna.PutAsJsonAsync($"/api/classification/tags/{tag}", new RenameEntryDto("Strom"), Ct)).StatusCode);

        var fields = await bob.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{annas}/fields", Ct);
        Assert.Equal("Strom", Assert.Single(fields!.Tags).Name);
    }

    [Fact]
    public async Task Merge_WithoutTheRight_Is403_AndWithItMovesTheDocuments()
    {
        var (anna, _) = await api.CreateMemberAsync("merge-anna", Ct);
        var (bob, _) = await api.CreateMemberAsync("merge-bob", Ct);
        var target = await anna.CreateEntryAsync("document-types", "Rechnung", Ct);
        var source = await anna.CreateEntryAsync("document-types", "Faktura", Ct);
        var document = await anna.UploadPdfAsync("faktura.pdf", isPrivate: false, Ct);
        await api.ClassifyAsync(document, Recognition.Fields(documentTypeId: source));
        var merge = new MergeEntriesDto(target, [source]);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/classification/document-types/merge", merge, Ct)).StatusCode);
        var bobsTarget = await bob.CreateEntryAsync("document-types", "Rechnung Bob", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/classification/document-types/merge", new MergeEntriesDto(bobsTarget, [source]), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anna.PostAsJsonAsync("/api/classification/document-types/merge", new MergeEntriesDto(target, [target]), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anna.PostAsJsonAsync("/api/classification/document-types/merge", merge, Ct)).StatusCode);

        var fields = await anna.GetFromJsonAsync<DocumentFieldsDto>($"/api/documents/{document}/fields", Ct);
        Assert.Equal(target, fields!.DocumentType?.Id);
        Assert.DoesNotContain((await anna.GetFromJsonAsync<EntryDto[]>("/api/classification/document-types", Ct))!, entry => entry.Id == source);
    }
}
