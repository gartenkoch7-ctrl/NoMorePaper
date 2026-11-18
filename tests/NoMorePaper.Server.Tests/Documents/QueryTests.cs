using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

public sealed class QueryTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_ShowsUploadedDocuments_NewestFirst()
    {
        var older = await UploadAsync("liste-aelter.pdf");
        await Task.Delay(20, Ct);
        var newer = await UploadAsync("liste-neuer.pdf");

        var list = await api.CreateClient().GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents?page=1&pageSize=100", Ct);

        Assert.NotNull(list);
        var ids = list.Select(d => d.Id).ToList();
        Assert.True(ids.IndexOf(newer) < ids.IndexOf(older));
        Assert.Contains(list, d => d.Id == newer && d.State == DocumentProcessingState.Queued && d.Title == "liste-neuer");
    }

    [Fact]
    public async Task List_AppliesPageSize_AndClampsNonsense()
    {
        await UploadAsync("seite-1.pdf");
        await UploadAsync("seite-2.pdf");
        var client = api.CreateClient();

        var one = await client.GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents?page=1&pageSize=1", Ct);
        var second = await client.GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents?page=2&pageSize=1", Ct);
        var clamped = await client.GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents?page=-4&pageSize=0", Ct);

        Assert.Single(one!);
        Assert.Single(second!);
        Assert.NotEqual(one![0].Id, second![0].Id);
        Assert.Single(clamped!);
    }

    [Fact]
    public async Task Detail_OfAnUnknownDocument_IsProblemDetails404()
    {
        using var response = await api.CreateClient().GetAsync($"/api/documents/{Guid.CreateVersion7()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("original")]
    [InlineData("other")]
    public async Task Download_OfAnUnknownDocument_Or_AMissingFile_Is404(string kind)
    {
        using var unknown = await api.CreateClient().GetAsync($"/api/documents/{Guid.CreateVersion7()}/files/{kind}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // An archive does not exist before processing; "other" is not a file kind at all.
        var id = await UploadAsync("ohne-archiv.pdf");
        using var existing = await api.CreateClient().GetAsync($"/api/documents/{id}/files/{kind}", Ct);
        Assert.Equal(kind == "original" ? HttpStatusCode.OK : HttpStatusCode.NotFound, existing.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsAnEmptyList()
    {
        var client = api.CreateClient();

        var none = await client.GetFromJsonAsync<SearchHitDto[]>("/api/search", Ct);
        var blank = await client.GetFromJsonAsync<SearchHitDto[]>("/api/search?q=%20%20", Ct);

        Assert.Empty(none!);
        Assert.Empty(blank!);
    }

    [Fact]
    public async Task Search_FindsFinishedDocuments_ButNotQueuedOnes()
    {
        var queued = await UploadAsync("suche-wartend.pdf");
        var done = await UploadAsync("suche-fertig.pdf");
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = await db.Documents.FindAsync([done], Ct);
            document!.StartProcessing();
            document.Complete("Die Zahnarztrechnung liegt bei.");
            await db.SaveChangesAsync(Ct);
        }

        var hits = await api.CreateClient().GetFromJsonAsync<SearchHitDto[]>("/api/search?q=Zahnarztrechnungen&limit=5", Ct);

        var hit = Assert.Single(hits!);
        Assert.Equal(done, hit.Id);
        Assert.Contains("Zahnarztrechnung", hit.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain(hits!, h => h.Id == queued);
    }

    private async Task<Guid> UploadAsync(string fileName)
    {
        using var response = await api.UploadAsync(fileName, SampleFiles.Pdf(), Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UploadResultDto>(Ct))!.Id;
    }
}
