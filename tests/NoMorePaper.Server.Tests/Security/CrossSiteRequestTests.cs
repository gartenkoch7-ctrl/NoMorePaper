using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Security;

public sealed class CrossSiteRequestTests(ApiFixture api)
{
    private const string Marker = "X-NoMorePaper-Request";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("none")]
    public async Task Post_FromAnotherSite_IsRefusedWith403_EvenWithTheMarker(string fetchSite)
    {
        using var request = Upload();
        request.Headers.Add("Sec-Fetch-Site", fetchSite);

        using var response = await api.CreateAdminClient().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_FromTheSameOrigin_IsAccepted_WithoutTheMarker()
    {
        using var client = api.CreateAdminClient();
        client.DefaultRequestHeaders.Remove(Marker);
        using var request = Upload();
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutFetchSiteAndMarker_IsRefused()
    {
        using var client = api.CreateAdminClient();
        client.DefaultRequestHeaders.Remove(Marker);
        using var request = Upload();

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_FromAnotherSite_IsServed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/documents");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await api.CreateAdminClient().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost", HttpStatusCode.OK)]
    [InlineData("http://127.0.0.1:8080", HttpStatusCode.OK)]
    [InlineData("http://evil.example", HttpStatusCode.BadRequest)]
    [InlineData("http://192.168.1.20", HttpStatusCode.BadRequest)]
    public async Task Requests_AreServedOnlyForTheAllowedHostNames(string baseAddress, HttpStatusCode expected)
    {
        using var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress) });

        using var response = await client.GetAsync(new Uri("/api/setup", UriKind.Relative), Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    private static HttpRequestMessage Upload() => new(HttpMethod.Post, "/api/documents")
    {
        Content = new MultipartFormDataContent { { new ByteArrayContent(SampleFiles.Pdf()), "file", "brief.pdf" } },
    };
}
