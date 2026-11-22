using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

public sealed class CountTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Counts_WithoutSignIn_Is401()
    {
        using var client = api.CreateClient();

        using var response = await client.GetAsync("/api/documents/counts", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Counts_WithABadFilter_IsProblemDetails400()
    {
        using var response = await api.CreateAdminClient().GetAsync("/api/documents/counts?from=gestern", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Counts_AndList_ShowTheVisibleDocuments()
    {
        // A database of its own: uploads of tests running in parallel would change the numbers.
        await using var app = api.WithFreshDatabase("query_counts");
        using var client = app.CreateClient();
        using var setup = await client.PostAsJsonAsync("/api/setup", new SetupDto("zaehler", "Zaehler", ApiFixture.AdminPassword), Ct);
        setup.EnsureSuccessStatusCode();
        await client.UploadPdfAsync("zaehler-1.pdf", isPrivate: false, Ct);
        await client.UploadPdfAsync("zaehler-2.pdf", isPrivate: false, Ct);
        await client.UploadPdfAsync("zaehler-3.pdf", isPrivate: true, Ct);

        var counts = await client.GetFromJsonAsync<DocumentCountsDto>("/api/documents/counts", Ct);
        var list = await client.GetFromJsonAsync<DocumentSummaryDto[]>("/api/documents", Ct);

        Assert.Equal((3, 3), (counts!.Total, counts.AllTypes));
        Assert.Empty(counts.ByType);
        Assert.All(list!, row =>
        {
            Assert.Equal("upload", row.Source);
            Assert.Null(row.Excerpt);
            Assert.Empty(row.Tags!);
        });
    }
}
