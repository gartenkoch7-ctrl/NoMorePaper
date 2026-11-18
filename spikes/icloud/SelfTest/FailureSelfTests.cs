using ICloudSpike.Auth;
using ICloudSpike.Http;

namespace ICloudSpike.SelfTest;

/// <summary>Every failure the user can run into must end in a clear category, not in a crash.</summary>
internal static class FailureSelfTests
{
    private const string Password = FakeAppleAccount.Password;

    public static IEnumerable<(string Name, Func<Task> Run)> All =>
    [
        ("Fehler: falsches Passwort (HTTP 401, Code -20101)", WrongPasswordAsync),
        ("Fehler: gesperrte Apple-ID (HTTP 403, Code -20209)", LockedAccountAsync),
        ("Fehler: Apple bremst bei der Anmeldung (HTTP 503)", ThrottledSignInAsync),
        ("Fehler: drei falsche Codes, danach Abbruch", WrongCodesAsync),
        ("Fehler: Abbruch bei der Code-Eingabe; Eingabe ohne Ziffern zählt nicht", CodePromptInputsAsync),
        ("Fehler: Erweiterter Datenschutz sperrt den Web-Zugriff", WebAccessBlockedAsync),
        ("Fehler: Sitzung läuft während der Nutzung ab (HTTP 421), Drosselung (HTTP 503)", DriveFailuresAsync),
        ("Fehler: keine Netzverbindung", OfflineAsync),
    ];

    private static async Task WrongPasswordAsync()
    {
        using var harness = new SpikeHarness();
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId], "wrong-" + FakeAppleAccount.SecretMarker);
        Check.Equal<ICloudFailure?>(ICloudFailure.WrongCredentials, outcome.Failure, "failure");
        Check.Equal<LoginPath?>(null, outcome.Path, "path");
        Check.Equal<string?>(null, harness.Store.Load().SessionToken, "session token after a rejected password");
        Check.Equal(0, harness.Server.Account.PushRequests, "code requested despite wrong password");
        harness.AssertNoSecretShown();
    }

    private static async Task LockedAccountAsync()
    {
        using var harness = new SpikeHarness();
        harness.Server.Account.Locked = true;
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId], Password);
        Check.Equal<ICloudFailure?>(ICloudFailure.AccountLocked, outcome.Failure, "failure");
    }

    private static async Task ThrottledSignInAsync()
    {
        using var harness = new SpikeHarness();
        harness.Server.Account.InitStatus = 503;
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId], Password);
        Check.Equal<ICloudFailure?>(ICloudFailure.RateLimited, outcome.Failure, "failure");
    }

    private static async Task WrongCodesAsync()
    {
        using var harness = new SpikeHarness();
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId, "111111", "222222", "333333"], Password);
        Check.Equal<ICloudFailure?>(ICloudFailure.TwoFactorFailed, outcome.Failure, "failure");
        Check.Equal<string?>(null, harness.Store.Load().TrustToken, "trust token without accepted code");
    }

    private static async Task CodePromptInputsAsync()
    {
        using (var cancelled = new SpikeHarness())
        {
            var outcome = await cancelled.RunAsync([SpikeHarness.TypedAppleId, string.Empty], Password);
            Check.Equal<ICloudFailure?>(ICloudFailure.Cancelled, outcome.Failure, "empty code input");
        }

        using var harness = new SpikeHarness();

        // Three inputs that are not codes, "n" for a new code, then the right code on the first real try.
        var accepted = await harness.RunAsync(
            [SpikeHarness.TypedAppleId, "abc", "12345", "1234567", "n", FakeAppleAccount.DeviceCode, string.Empty],
            Password);
        Check.Equal<LoginPath?>(LoginPath.PasswordAndCode, accepted.Path, "path");
        Check.Equal(2, harness.Server.Account.PushRequests, "code requests after \"n\"");
    }

    private static async Task WebAccessBlockedAsync()
    {
        using var harness = new SpikeHarness();
        harness.Server.Account.WebAccessAllowed = false;
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId, FakeAppleAccount.DeviceCode], Password);
        Check.Equal<ICloudFailure?>(ICloudFailure.WebAccessBlocked, outcome.Failure, "failure");
        Check.Equal<LoginPath?>(LoginPath.PasswordAndCode, outcome.Path, "sign-in itself must still succeed");
        Check.That(
            harness.Transcript.Exists(line => line.Contains("Erweiterter Datenschutz: ja", StringComparison.Ordinal)),
            "account report does not show Advanced Data Protection");
    }

    private static async Task DriveFailuresAsync()
    {
        using var harness = new SpikeHarness();
        harness.Server.Account.DriveStatus = 421;
        var expired = await harness.RunAsync([SpikeHarness.TypedAppleId, FakeAppleAccount.DeviceCode], Password);
        Check.Equal<ICloudFailure?>(ICloudFailure.SessionExpired, expired.Failure, "HTTP 421 on the listing");
        Check.Equal<int?>(null, expired.RootItemCount, "root entries");

        harness.Server.Account.DriveStatus = 503;
        var throttled = await harness.RunAsync([]);
        Check.Equal<ICloudFailure?>(ICloudFailure.RateLimited, throttled.Failure, "HTTP 503 on the listing");
        Check.Equal<LoginPath?>(LoginPath.ReusedSession, throttled.Path, "session must survive a failed listing");
    }

    private static async Task OfflineAsync()
    {
        using var harness = new SpikeHarness();
        harness.Server.Offline = true;
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId], Password);
        Check.Equal<ICloudFailure?>(ICloudFailure.Network, outcome.Failure, "failure");
    }
}
