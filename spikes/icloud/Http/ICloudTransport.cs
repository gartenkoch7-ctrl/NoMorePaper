using System.Net;
using System.Text;

namespace ICloudSpike.Http;

/// <summary>
/// HTTP access to Apple's endpoints with browser-like headers, an own cookie jar and a trace that
/// shows method, host, path and status only (no query strings, headers, bodies or cookie values).
/// </summary>
internal sealed class ICloudTransport : IDisposable
{
    public const string HomeUrl = "https://www.icloud.com";

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
        + "Chrome/138.0.0.0 Safari/537.36";

    private const int MaxRedirects = 5;

    private readonly HttpClient _client;
    private readonly Action<string> _trace;

    public ICloudTransport(HttpMessageHandler handler, Action<string> trace)
    {
        _client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(60) };
        _trace = trace;
    }

    /// <summary>
    /// Apple sets more and larger cookies per domain than the .NET defaults (20 cookies, 4 KB each)
    /// allow; with the defaults some would be dropped silently and the session would not survive.
    /// </summary>
    public CookieContainer Cookies { get; } = new(capacity: 500, perDomainCapacity: 100, maxCookieSize: 32 * 1024);

    /// <summary>
    /// Cookies and redirects are handled here instead of in the handler: the cookie jar must be
    /// saved between runs, and a redirect must never carry Apple cookies to another host unseen.
    /// </summary>
    public static SocketsHttpHandler CreateNetworkHandler() => new()
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
    };

    public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(request.Method, request.Uri);
        AddBrowserHeaders(message);
        foreach (var (name, value) in request.Headers)
        {
            message.Headers.Remove(name);
            message.Headers.TryAddWithoutValidation(name, value);
        }

        var cookies = Cookies.GetCookieHeader(request.Uri);
        if (cookies.Length > 0)
        {
            message.Headers.TryAddWithoutValidation("Cookie", cookies);
        }

        if (request.Body is not null)
        {
            message.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.Body));
            message.Content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);
        }

        using var response = await SendCoreAsync(
            message, Describe(request.Uri), HttpCompletionOption.ResponseContentRead, cancellationToken);
        StoreCookies(request.Uri, response);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ApiResponse((int)response.StatusCode, headers, body);
    }

    /// <summary>
    /// Streams a file from Apple's content servers. The address is pre-signed, so neither cookies
    /// nor Apple headers are sent.
    /// </summary>
    public async Task<long> DownloadAsync(Uri uri, Stream target, CancellationToken cancellationToken)
    {
        var current = uri;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (current.Scheme != Uri.UriSchemeHttps)
            {
                throw new ICloudException(ICloudFailure.ProtocolChanged, "download: redirect to an unencrypted address.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, current);
            message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            // Host only: the path of a pre-signed address is itself a secret.
            using var response = await SendCoreAsync(
                message, current.Host + "/…", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw ResponseErrors.Create(
                    "download", new ApiResponse(status, new Dictionary<string, string>(), string.Empty));
            }

            var before = target.Position;
            try
            {
                await response.Content.CopyToAsync(target, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                throw new ICloudException(
                    ICloudFailure.Network, "download: connection lost while reading the file.", innerException: exception);
            }

            return target.Position - before;
        }

        throw new ICloudException(ICloudFailure.ProtocolChanged, "download: too many redirects.");
    }

    public void Dispose() => _client.Dispose();

    private static void AddBrowserHeaders(HttpRequestMessage message)
    {
        message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        message.Headers.TryAddWithoutValidation("Accept", "*/*");
        message.Headers.TryAddWithoutValidation("Origin", HomeUrl);
        message.Headers.TryAddWithoutValidation("Referer", HomeUrl + "/");
    }

    /// <summary>Host and path of an API call; the query (account id, client id) is left out.</summary>
    private static string Describe(Uri uri) => uri.Host + uri.AbsolutePath;

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage message,
        string target,
        HttpCompletionOption completion,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SendAsync(message, completion, cancellationToken);
            _trace($"{message.Method} {target} -> {(int)response.StatusCode}");
            return response;
        }
        catch (HttpRequestException exception)
        {
            _trace($"{message.Method} {target} -> network error");
            throw new ICloudException(
                ICloudFailure.Network, $"{target}: no connection.", innerException: exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _trace($"{message.Method} {target} -> timeout");
            throw new ICloudException(ICloudFailure.Network, $"{target}: timeout.", innerException: exception);
        }
    }

    private void StoreCookies(Uri uri, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            try
            {
                Cookies.SetCookies(uri, value);
            }
            catch (CookieException)
            {
                // Only the name is shown; a rejected cookie is the first suspect if the session
                // does not survive a restart.
                var name = value.Split('=', 2)[0];
                _trace($"cookie rejected by the .NET cookie jar: {name}");
            }
        }
    }
}
