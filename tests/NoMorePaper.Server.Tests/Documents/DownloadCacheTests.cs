using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Documents;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Classification;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

/// <summary>
/// Downloads after an edit (M4b): the edited version is served, and every file carries its hash as ETag
/// and must be revalidated, so viewer and thumbnails never show an old version. Deleting goes to the trash.
/// </summary>
public sealed class DownloadCacheTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Download_CarriesTheHashAsETag_AndMustBeRevalidated()
    {
        var bytes = SampleFiles.WithHead("%PDF-1.4 "u8, totalLength: 2_000);
        var id = await UploadAsync(bytes);
        var client = api.CreateAdminClient();

        using var response = await client.GetAsync($"/api/documents/{id}/files/original", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"\"{Convert.ToHexStringLower(SHA256.HashData(bytes))}\"", response.Headers.ETag?.Tag);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.True(response.Headers.CacheControl?.Private);

        using var again = new HttpRequestMessage(HttpMethod.Get, $"/api/documents/{id}/files/original");
        again.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        using var unchanged = await client.SendAsync(again, Ct);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
    }

    [Fact]
    public async Task TheEditedVersion_IsServed_OnceItExists()
    {
        var id = await UploadAsync(SampleFiles.WithHead("%PDF-1.4 "u8, totalLength: 1_000));
        var client = api.CreateAdminClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{id}/files/edited", Ct)).StatusCode);
        var edited = "%PDF-1.7 bearbeitet"u8.ToArray();
        var editedAt = new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
        await StoreEditedAsync(id, edited, editedAt);

        using var response = await client.GetAsync($"/api/documents/{id}/files/edited", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(edited, await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal($"\"{Convert.ToHexStringLower(SHA256.HashData(edited))}\"", response.Headers.ETag?.Tag);
        var detail = await client.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{id}", Ct);
        Assert.Equal((edited.Length, editedAt, false), (detail!.Edited?.SizeBytes, detail.EditedAt, detail.EditPending));
        Assert.NotNull(detail.Original);
    }

    [Fact]
    public async Task DeletingAPossibleDuplicate_MovesItToTheTrash()
    {
        var original = await UploadAsync(SampleFiles.Pdf());
        var copy = await UploadAsync(SampleFiles.Pdf());
        await api.MarkDuplicateAsync(copy, original);
        var client = api.CreateAdminClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{copy}", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{copy}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{copy}/files/original", Ct)).StatusCode);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trashed = await db.Documents.IgnoreQueryFilters().AsNoTracking().SingleAsync(document => document.Id == copy, Ct);
        Assert.True(trashed.IsInTrash);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM trash_items WHERE document_id = {copy}").SingleAsync(Ct));
    }

    private async Task<Guid> UploadAsync(byte[] bytes)
    {
        using var upload = await api.UploadAsync("brief.pdf", bytes, Ct);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        return (await upload.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
    }

    // Plays the Worker's part of an edit: the file in the archive and its record.
    private async Task StoreEditedAsync(Guid id, byte[] bytes, DateTimeOffset at)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().SaveEditedAsync(id, new MemoryStream(bytes), Ct);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.Documents.SingleAsync(found => found.Id == id, Ct);
        document.ReplaceFile(new DocumentFile(DocumentFileKind.Edited, "brief.pdf", "application/pdf", stored.SizeBytes, stored.Sha256), at);
        await db.SaveChangesAsync(Ct);
    }
}
