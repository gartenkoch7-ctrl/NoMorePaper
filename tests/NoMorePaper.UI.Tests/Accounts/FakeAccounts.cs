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
                    ? JsonSerializer.Serialize(Me, _json)
                    : "{}",
            }));
        await Mocks.RespondAsync(page, "POST", "/api/auth/login", LoginStatus, null,
            RetryAfterSeconds is null ? null : new Dictionary<string, string> { ["Retry-After"] = RetryAfterSeconds },
            () => _signedIn = LoginStatus == 204);
        await Mocks.RespondAsync(page, "POST", "/api/auth/logout(-everywhere)?", 204, onCall: () => _signedIn = false);
    }
}
