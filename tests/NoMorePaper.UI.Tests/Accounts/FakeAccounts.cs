using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>
/// A small in-browser stand-in for the sign-in part of the accounts API: "who am I" answers 401
/// until a login call succeeded, and 204 on logout flips it back. Everything else (members,
/// invitations, ...) is mocked per test with <see cref="AccountMocks"/>.
/// </summary>
internal sealed class FakeAccounts
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private volatile bool _signedIn;

    public AccountMocks Mocks { get; } = new();

    public bool IsAdmin { get; init; } = true;

    public bool SetupNeeded { get; init; }

    /// <summary>Status of the login answer: 204 signs in, 401 is a wrong password, 423 a lock.</summary>
    public int LoginStatus { get; set; } = 204;

    public string? RetryAfterSeconds { get; set; }

    /// <summary>Unconfirmed resets by the administrator's link that "who am I" reports (<c>adminResets</c>).</summary>
    public IReadOnlyList<DateTimeOffset>? AdminResets { get; init; }

    /// <summary>What the server does with a successful setup or accepted invitation: it signs the account in.</summary>
    public void MarkSignedIn() => _signedIn = true;

    public AccountDto Me => new(Guid.Parse("00000000-0000-0000-0000-000000000042"), "anna", "Anna Beispiel", IsAdmin);

    /// <summary>A page that starts signed out, or signed in when <paramref name="signedIn"/> is true.</summary>
    public async Task InstallAsync(IPage page, bool signedIn = false)
    {
        _signedIn = signedIn;
        await Mocks.RespondAsync(page, "GET", "/api/setup", 200, new SetupStatusDto(SetupNeeded));
        await page.RouteAsync(new Regex("/api/auth/me$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : route.FulfillAsync(new RouteFulfillOptions
            {
                Status = _signedIn ? 200 : 401,
                ContentType = _signedIn ? "application/json" : "application/problem+json",
                Body = _signedIn
                    ? MeJson()
                    : "{}",
            }));
        await Mocks.RespondAsync(page, "POST", "/api/auth/login", LoginStatus, null,
            RetryAfterSeconds is null ? null : new Dictionary<string, string> { ["Retry-After"] = RetryAfterSeconds },
            () => _signedIn = LoginStatus == 204);
        await Mocks.RespondAsync(page, "POST", "/api/auth/logout(-everywhere)?", 204, onCall: () => _signedIn = false);
        // The navigation asks for the number of documents to review on every page (the list only on
        // "Zu prüfen"); without a real session the server would answer 401 and the app would sign out.
        await page.RouteAsync(new Regex("/api/review$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : route.FulfillAsync(new RouteFulfillOptions
            {
                Status = _signedIn ? 200 : 401,
                ContentType = _signedIn ? "application/json" : "application/problem+json",
                Body = _signedIn ? "[]" : "{}",
            }));
        // The account page and the upload page read the account's preferences; a test about them mocks its own
        // answer (registered later, so it wins).
        await page.RouteAsync(new Regex("/api/account/preferences$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : route.FulfillAsync(new RouteFulfillOptions
            {
                Status = _signedIn ? 200 : 401,
                ContentType = _signedIn ? "application/json" : "application/problem+json",
                Body = _signedIn ? "{\"newDocumentsPrivate\":false,\"mailSenders\":[]}" : "{}",
            }));
        await page.RouteAsync(new Regex("/api/review/count$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : route.FulfillAsync(new RouteFulfillOptions
            {
                Status = _signedIn ? 200 : 401,
                ContentType = _signedIn ? "application/json" : "application/problem+json",
                Body = _signedIn ? "0" : "{}",
            }));
    }

    // With resets, the answer also carries the oldest one as adminResetAt, as the server does.
    private string MeJson() => AdminResets is not { Count: > 0 } resets
        ? JsonSerializer.Serialize(Me, _json)
        : JsonSerializer.Serialize(new { Me.Id, Me.UserName, Me.DisplayName, Me.IsAdmin, AdminResetAt = resets.Min(), AdminResets = resets }, _json);
}
