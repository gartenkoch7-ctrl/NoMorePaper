using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>One call the page made to a mocked route.</summary>
internal sealed record RecordedRequest(string Method, string Path, string? Body);

/// <summary>
/// Answers account API calls inside the browser and records what the page sent, so the pages can
/// be tested without the real accounts backend (and without locking real accounts).
/// </summary>
internal sealed class AccountMocks
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public IReadOnlyCollection<RecordedRequest> Requests => _requests;

    /// <summary>The body of the last call to <paramref name="method"/> <paramref name="path"/>, or null.</summary>
    public string? BodyOf(string method, string path) => _requests
        .LastOrDefault(r => r.Method == method && r.Path.EndsWith(path, StringComparison.Ordinal))?.Body;

    public bool WasCalled(string method, string path) => _requests
        .Any(r => r.Method == method && r.Path.EndsWith(path, StringComparison.Ordinal));

    /// <summary>
    /// Answers <paramref name="method"/> calls whose path ends with <paramref name="path"/>
    /// (a regex). <paramref name="body"/> may be a function of the call for answers that change.
    /// </summary>
    public Task RespondAsync(
        IPage page, string method, string path, int status, object? body = null,
        IDictionary<string, string>? headers = null, Action? onCall = null)
        => page.RouteAsync(new Regex($"{path}$"), route =>
        {
            var request = route.Request;
            if (request.Method != method)
            {
                return route.FallbackAsync();
            }

            _requests.Enqueue(new RecordedRequest(method, new Uri(request.Url).AbsolutePath, request.PostData));
            onCall?.Invoke();
            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = status,
                ContentType = status >= 400 ? "application/problem+json" : "application/json",
                Headers = headers,
                Body = status == 204 ? null : JsonSerializer.Serialize(body ?? new { }, _json),
            });
        });
}
