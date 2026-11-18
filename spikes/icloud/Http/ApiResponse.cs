using System.Text.Json;

namespace ICloudSpike.Http;

/// <summary>Status, headers and body of an answer, detached from the network connection.</summary>
internal sealed class ApiResponse
{
    private readonly Dictionary<string, string> _headers;
    private JsonElement? _json;
    private bool _jsonRead;

    public ApiResponse(int status, Dictionary<string, string> headers, string body)
    {
        Status = status;
        _headers = headers;
        Body = body;
    }

    public int Status { get; }

    public string Body { get; }

    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>The body as JSON, or null if it is empty or something else (e.g. an HTML error page).</summary>
    public JsonElement? Json
    {
        get
        {
            if (!_jsonRead)
            {
                _jsonRead = true;
                _json = TryParse(Body);
            }

            return _json;
        }
    }

    public string? Header(string name) =>
        _headers.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    private static JsonElement? TryParse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
