using System.Net;
using System.Text;

namespace ICloudSpike.SelfTest;

/// <summary>Builds answers of the simulated Apple server.</summary>
internal static class FakeResponses
{
    public static HttpResponseMessage Json(int status, string json, params (string Name, string Value)[] headers) =>
        Create(status, json, "application/json", headers);

    public static HttpResponseMessage Html(int status, string html, params (string Name, string Value)[] headers) =>
        Create(status, html, "text/html", headers);

    public static HttpResponseMessage Empty(int status, params (string Name, string Value)[] headers) =>
        Create(status, string.Empty, "text/plain", headers);

    public static HttpResponseMessage Bytes(byte[] content) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    public static HttpResponseMessage Redirect(Uri location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = location;
        return response;
    }

    private static HttpResponseMessage Create(
        int status, string body, string mediaType, (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
}
