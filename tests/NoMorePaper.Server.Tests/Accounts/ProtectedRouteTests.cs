using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

/// <summary>Without sign-in no route that holds data answers with data.</summary>
public sealed class ProtectedRouteTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> ProtectedRoutes => new()
    {
        { "GET", "/api/documents" },
        { "GET", "/api/documents/{id}" },
        { "GET", "/api/documents/{id}/files/original" },
        { "PATCH", "/api/documents/{id}" },
        { "POST", "/api/documents" },
        { "GET", "/api/search?q=aus" },
        { "GET", "/api/auth/me" },
        { "POST", "/api/auth/logout-everywhere" },
        { "POST", "/api/account/password" },
        { "POST", "/api/invitations" },
        { "GET", "/api/members" },
        { "DELETE", "/api/members/{id}" },
        { "POST", "/api/members/{id}/reset-link" },
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task WithoutSignIn_ProtectedRoutes_Answer401ProblemDetails(string method, string route)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{id}", api.LegacyDocumentId.ToString(), StringComparison.Ordinal))
        {
            Content = method is "POST" or "PATCH" ? JsonContent.Create(new DocumentPrivacyDto(true)) : null,
        };

        using var response = await api.CreateClient().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Location"));
    }

    [Theory]
    [InlineData("/api/setup")]
    [InlineData("/health")]
    public async Task WithoutSignIn_SetupAndHealth_AreServed(string path)
    {
        using var response = await api.CreateClient().GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
