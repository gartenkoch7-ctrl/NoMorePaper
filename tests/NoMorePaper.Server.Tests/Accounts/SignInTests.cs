using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Accounts;

public sealed class SignInTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_SetsAnHttpOnlyStrictCookie_ThatLastsThirtyDays()
    {
        await api.CreateMemberAsync("cookie-member", Ct);

        var (client, response) = await api.SignInAsync("COOKIE-MEMBER", ApiFixtureExtensions.MemberPassword, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = SessionCookie.SetCookieHeader(response);
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(Expires(cookie) - DateTimeOffset.UtcNow, TimeSpan.FromDays(29.9), TimeSpan.FromDays(30.1));
        var me = await client.GetFromJsonAsync<AccountDto>("/api/auth/me", Ct);
        Assert.Equal(("cookie-member", false), (me!.UserName, me.IsAdmin));
    }

    [Fact]
    public async Task SignIn_WithAWrongPassword_OrAnUnknownName_IsTheSame401()
    {
        await api.CreateMemberAsync("wrong-member", Ct);

        var (_, wrong) = await api.SignInAsync("wrong-member", "not-the-password", Ct);
        var (_, unknown) = await api.SignInAsync("nobody-here", "not-the-password", Ct);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (wrong.StatusCode, unknown.StatusCode));
        Assert.Null(SessionCookie.SetCookieHeader(wrong));
        var first = await wrong.Content.ReadFromJsonAsync<ProblemBody>(Ct);
        var second = await unknown.Content.ReadFromJsonAsync<ProblemBody>(Ct);
        Assert.Equal(first!.Title, second!.Title);
    }

    [Fact]
    public async Task SignIn_LocksTheAccountAfterFiveFailures_For423WithRetryAfter_EvenWithTheRightPassword()
    {
        await api.CreateMemberAsync("locked-member", Ct);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            statuses.Add((await api.SignInAsync("locked-member", "wrong-password", Ct)).Response.StatusCode);
        }

        var (_, right) = await api.SignInAsync("locked-member", ApiFixtureExtensions.MemberPassword, Ct);

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 4), HttpStatusCode.Locked], statuses);
        Assert.Equal(HttpStatusCode.Locked, right.StatusCode);
        Assert.Equal("application/problem+json", right.Content.Headers.ContentType?.MediaType);
        var retryAfter = right.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
        Assert.Null(SessionCookie.SetCookieHeader(right));
    }

    [Fact]
    public async Task SignIn_ParallelGuesses_CannotSlipPastTheLockout()
    {
        await api.CreateMemberAsync("parallel-member", Ct);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => api.SignInAsync("parallel-member", "wrong-password", Ct)));

        // The row lock serializes the attempts: exactly four are judged before the fifth locks the account.
        Assert.Equal(4, attempts.Count(attempt => attempt.Response.StatusCode == HttpStatusCode.Unauthorized));
        Assert.Equal(8, attempts.Count(attempt => attempt.Response.StatusCode == HttpStatusCode.Locked));
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        var (member, _) = await api.CreateMemberAsync("logout-member", Ct);

        using var signOut = await member.PostAsync("/api/auth/logout", null, Ct);
        using var me = await member.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task SignOut_WithoutSession_IsHarmless()
    {
        using var response = await api.CreateClient().PostAsync("/api/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static DateTimeOffset Expires(string setCookie)
    {
        var attribute = setCookie.Split(';').Select(part => part.Trim())
            .Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        return DateTimeOffset.Parse(attribute["expires=".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }
}
