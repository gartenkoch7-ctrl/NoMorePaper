using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Trash;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Trash;

/// <summary>
/// <c>/api/trash</c> and <c>POST /api/documents/{id}/trash</c> (ADR-0025): only the owner moves a document there,
/// sees its entries, restores them and removes them for good; everybody else gets 404 or 403.
/// </summary>
public sealed class TrashApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOwner_MovesADocumentToTheTrash_SeesIt_AndRestoresIt_WithItsJob()
    {
        var (owner, _) = await api.CreateMemberAsync("trash-owner", Ct);
        var id = await owner.UploadPdfAsync("brief.pdf", isPrivate: false, Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/documents/{id}/trash", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/documents/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/documents/{id}/trash", null, Ct)).StatusCode);
        Assert.False(await HasJobAsync(id));
        var json = await owner.GetStringAsync("/api/trash", Ct);
        Assert.Contains("\"kind\":\"Document\"", json, StringComparison.Ordinal);
        var item = Assert.Single((await owner.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!);
        Assert.Equal((TrashItemKindDto.Document, "brief", false, false), (item.Kind, item.Title, item.RestoreSwaps, item.DocumentInTrash));
        Assert.Equal(item.DeletedAt.AddDays(30), item.PurgeAfter);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/trash/{item.Id}/restore", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/documents/{id}", Ct)).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!);
        Assert.True(await HasJobAsync(id));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/trash/{item.Id}/restore", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task OtherAccounts_NeitherSeeNorTouchTheEntries()
    {
        var (owner, _) = await api.CreateMemberAsync("trash-anna", Ct);
        var (other, otherId) = await api.CreateMemberAsync("trash-bob", Ct);
        var shared = await owner.UploadPdfAsync("geteilt.pdf", isPrivate: false, Ct);
        var editable = await owner.UploadPdfAsync("bearbeitbar.pdf", isPrivate: false, Ct);
        using var editors = await owner.PutAsJsonAsync(
            $"/api/documents/{editable}/editors", new SetEditorsRequestDto([otherId]), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/documents/{shared}/trash", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/documents/{editable}/trash", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/documents/{shared}/trash", null, Ct)).StatusCode);
        var item = Assert.Single((await owner.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!);

        Assert.Empty((await other.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/trash/{item.Id}/restore", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/trash/{item.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await other.DeleteAsync("/api/trash", Ct)).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/documents/{shared}", Ct)).StatusCode);
    }

    [Fact]
    public async Task RemovingForGood_AndEmptying_DeleteTheDocuments()
    {
        var (owner, _) = await api.CreateMemberAsync("trash-purge", Ct);
        var first = await owner.UploadPdfAsync("eins.pdf", isPrivate: true, Ct);
        var second = await owner.UploadPdfAsync("zwei.pdf", isPrivate: true, Ct);
        var third = await owner.UploadPdfAsync("drei.pdf", isPrivate: true, Ct);
        foreach (var id in new[] { first, second, third })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/documents/{id}/trash", null, Ct)).StatusCode);
        }

        var entries = (await owner.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!;
        Assert.Equal(["drei", "zwei", "eins"], entries.Select(entry => entry.Title));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/trash/{entries[0].Id}", Ct)).StatusCode);
        Assert.False(await ExistsAsync(third));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/trash/{entries[0].Id}", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync("/api/trash", Ct)).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<TrashItemDto[]>("/api/trash", Ct))!);
        Assert.False(await ExistsAsync(first) || await ExistsAsync(second));
    }

    [Fact]
    public async Task WithoutSignIn_EveryRouteIs401()
    {
        using var anonymous = api.CreateClient();
        var id = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/trash", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/trash/{id}/restore", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/trash/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync("/api/trash", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/documents/{id}/trash", null, Ct)).StatusCode);
    }

    private async Task<bool> HasJobAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProcessingJobs.AnyAsync(job => job.DocumentId == id, Ct);
    }

    private async Task<bool> ExistsAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Documents.IgnoreQueryFilters().AnyAsync(document => document.Id == id, Ct);
    }
}
