namespace NoMorePaper.Server.Tests.Hosting;

internal static class ApiFixtureExtensions
{
    /// <summary>Posts the bytes as multipart form field <c>file</c>, the way the browser does.</summary>
    public static async Task<HttpResponseMessage> UploadAsync(
        this ApiFixture api, string fileName, byte[] bytes, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        return await api.CreateClient().PostAsync("/api/documents", content, cancellationToken).ConfigureAwait(false);
    }
}
