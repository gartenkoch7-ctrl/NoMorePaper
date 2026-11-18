using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Documents;
using Xunit;

namespace NoMorePaper.Server.Tests.Hosting;

internal static class ApiFixtureExtensions
{
    public const string MemberPassword = "member-password-1";

    /// <summary>Posts the bytes as the administrator, as multipart form field <c>file</c>, the way the browser does.</summary>
    public static async Task<HttpResponseMessage> UploadAsync(
        this ApiFixture api, string fileName, byte[] bytes, CancellationToken cancellationToken)
    {
        using var client = api.CreateAdminClient();
        return await client.UploadAsync(fileName, bytes, isPrivate: null, cancellationToken).ConfigureAwait(false);
    }

    /// <param name="isPrivate">Value of form field <c>private</c>; <c>null</c> leaves it out.</param>
    public static async Task<HttpResponseMessage> UploadAsync(
        this HttpClient client, string fileName, byte[] bytes, string? isPrivate, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        if (isPrivate is not null)
        {
            content.Add(new StringContent(isPrivate), "private");
        }

        return await client.PostAsync("/api/documents", content, cancellationToken).ConfigureAwait(false);
    }

    /// <returns>The id of the accepted upload.</returns>
    public static async Task<Guid> UploadPdfAsync(this HttpClient client, string fileName, bool isPrivate, CancellationToken cancellationToken)
    {
        using var response = await client.UploadAsync(fileName, SampleFiles.Pdf(), isPrivate ? "true" : "false", cancellationToken)
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UploadResultDto>(cancellationToken).ConfigureAwait(false))!.Id;
    }

    /// <returns>A client that keeps the cookies it receives, signed in when the answer is 204.</returns>
    public static async Task<(HttpClient Client, HttpResponseMessage Response)> SignInAsync(
        this WebApplicationFactory<Program> app, string userName, string password, CancellationToken cancellationToken)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new SignInDto(userName, password), cancellationToken)
            .ConfigureAwait(false);
        return (client, response);
    }

    /// <summary>Invites a member as the administrator and accepts the invitation.</summary>
    /// <returns>A client signed in as the new member, and its id.</returns>
    public static async Task<(HttpClient Client, Guid Id)> CreateMemberAsync(
        this ApiFixture api, string userName, CancellationToken cancellationToken)
    {
        using var admin = api.CreateAdminClient();
        var token = await admin.InviteAsync(cancellationToken).ConfigureAwait(false);
        var member = api.CreateClient();
        using var accepted = await member.PostAsJsonAsync(
            $"/api/invitations/{token}/accept", new AcceptInvitationDto(userName, userName + " Name", MemberPassword), cancellationToken)
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var me = await member.GetFromJsonAsync<AccountDto>("/api/auth/me", cancellationToken).ConfigureAwait(false);
        return (member, me!.Id);
    }

    /// <returns>The token of a new invitation, taken from its link.</returns>
    public static async Task<string> InviteAsync(this HttpClient admin, CancellationToken cancellationToken)
    {
        using var response = await admin.PostAsync("/api/invitations", null, cancellationToken).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<LinkDto>(cancellationToken).ConfigureAwait(false);
        return TokenOf(link!.Link);
    }

    public static string TokenOf(string link) => new Uri(link).Segments[^1];
}
