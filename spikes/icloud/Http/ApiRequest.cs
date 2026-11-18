namespace ICloudSpike.Http;

/// <summary>One call to an Apple endpoint.</summary>
internal sealed record ApiRequest(HttpMethod Method, Uri Uri)
{
    public const string JsonContent = "application/json";

    /// <summary>What the iCloud web app sends for its service calls, even though the body is JSON.</summary>
    public const string PlainTextContent = "text/plain;charset=UTF-8";

    public string? Body { get; init; }

    public string ContentType { get; init; } = JsonContent;

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Builds a URI with escaped query parameters.</summary>
    public static Uri BuildUri(string baseUrl, IEnumerable<KeyValuePair<string, string>> query) =>
        new(AppendQuery(baseUrl, query));

    /// <summary>Appends escaped query parameters to an address or a path.</summary>
    public static string AppendQuery(string address, IEnumerable<KeyValuePair<string, string>> query)
    {
        var pairs = query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        var queryText = string.Join('&', pairs);
        return queryText.Length == 0 ? address : $"{address}?{queryText}";
    }
}
