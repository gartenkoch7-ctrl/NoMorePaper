using System.Security.Cryptography;
using System.Text;
using ICloudSpike.Auth;

namespace ICloudSpike.SelfTest;

/// <summary>The spike's four criteria, played through against the simulated Apple server.</summary>
internal static class FlowSelfTests
{
    private const string Password = FakeAppleAccount.Password;

    public static IEnumerable<(string Name, Func<Task> Run)> All =>
    [
        ("Ablauf: Erstanmeldung mit Code, Auflisten, Laden, Neustart ohne Eingabe", FirstLoginThenRestartAsync),
        ("Ablauf: abgelaufene Cookies werden mit dem Token erneuert", ExpiredCookiesAreRenewedAsync),
        ("Ablauf: abgelaufenes Token – Passwort ohne Code dank Trust-Token", ExpiredTokenNeedsPasswordOnlyAsync),
        ("Ablauf: Code per SMS, Passwortprotokoll s2k_fo", SmsCodeAsync),
        ("Ablauf: Unterordner, leere Datei, Dateiname vom Server wird entschärft", SubFolderAndNamesAsync),
        ("Sitzungsdatei: verschlüsselt, vollständig, beschädigte Datei wird verkraftet", SessionFileAsync),
    ];

    private static async Task FirstLoginThenRestartAsync()
    {
        using var harness = new SpikeHarness();

        // Wrong code first, then the right one, then entry 2 of the root folder (a PDF).
        var first = await harness.RunAsync([SpikeHarness.TypedAppleId, "000000", "123 456", "2"], Password);
        Check.Equal<object?>(null, first.Failure, "first run failure");
        Check.Equal<LoginPath?>(LoginPath.PasswordAndCode, first.Path, "first run path");
        Check.Equal<int?>(3, first.RootItemCount, "root entries");
        Check.Equal(1, harness.Server.Account.PushRequests, "code requests to trusted devices");
        Check.That(first.Download is not null, "no file downloaded");
        Check.Equal(FakeCloudHost.FileContent.LongLength, first.Download!.Bytes, "downloaded size");
        Check.Equal(
            Convert.ToHexStringLower(SHA256.HashData(FakeCloudHost.FileContent)), first.Download.Sha256, "downloaded hash");
        Check.That(
            File.ReadAllBytes(first.Download.LocalPath).AsSpan().SequenceEqual(FakeCloudHost.FileContent),
            "downloaded content differs");
        Check.That(!harness.Server.Cloud.ContentRequestLeakedHeaders, "cookies or Origin sent to the content server");

        // Restart: no prompt may appear (the script has no Apple-ID, password or code to give).
        var second = await harness.RunAsync([string.Empty]);
        Check.Equal<LoginPath?>(LoginPath.ReusedSession, second.Path, "second run path");
        Check.Equal<int?>(3, second.RootItemCount, "root entries after restart");
        Check.Equal(0, harness.Server.CallsTo("idmsa.apple.com"), "sign-in calls after restart");
        harness.AssertNoSecretShown();
    }

    private static async Task ExpiredCookiesAreRenewedAsync()
    {
        using var harness = new SpikeHarness();
        await harness.RunAsync([SpikeHarness.TypedAppleId, FakeAppleAccount.DeviceCode, string.Empty], Password);

        harness.Server.Account.ExpireCookies();
        var renewed = await harness.RunAsync([string.Empty]);
        Check.Equal<LoginPath?>(LoginPath.RenewedByToken, renewed.Path, "path with expired cookies");
        Check.Equal(0, harness.Server.CallsTo("idmsa.apple.com"), "sign-in calls");

        // The renewed cookies must have been saved: the next start works on cookies alone.
        var again = await harness.RunAsync([string.Empty]);
        Check.Equal<LoginPath?>(LoginPath.ReusedSession, again.Path, "path after renewal");
    }

    private static async Task ExpiredTokenNeedsPasswordOnlyAsync()
    {
        using var harness = new SpikeHarness();
        await harness.RunAsync([SpikeHarness.TypedAppleId, FakeAppleAccount.DeviceCode, string.Empty], Password);

        harness.Server.Account.ExpireSession();
        var outcome = await harness.RunAsync([SpikeHarness.TypedAppleId, string.Empty], Password);
        Check.Equal<LoginPath?>(LoginPath.PasswordOnly, outcome.Path, "path with expired token");
        Check.Equal(1, harness.Server.Account.PushRequests, "no second code request");
        Check.Equal<int?>(3, outcome.RootItemCount, "root entries");
    }

    private static async Task SmsCodeAsync()
    {
        using var harness = new SpikeHarness();
        harness.Server.Account.Protocol = ApplePasswordKey.ProtocolS2kFo;

        // "s" asks for an SMS, "2" picks the second of two numbers, then the SMS code.
        var outcome = await harness.RunAsync(
            [SpikeHarness.TypedAppleId, "s", "2", FakeAppleAccount.SmsCode, string.Empty], Password);
        Check.Equal<LoginPath?>(LoginPath.PasswordAndCode, outcome.Path, "path");
        Check.Equal(1, harness.Server.Account.SmsRequests, "SMS requests");
        harness.AssertNoSecretShown();
    }

    private static async Task SubFolderAndNamesAsync()
    {
        using var harness = new SpikeHarness();

        // Entry 1 is the folder "Scans"; its only file has a name that tries to leave the folder.
        var nested = await harness.RunAsync(
            [SpikeHarness.TypedAppleId, FakeAppleAccount.DeviceCode, "x", "1", "1"], Password);
        Check.That(nested.Download is not null, "no file downloaded from the sub-folder");
        Check.Equal(
            Path.GetFullPath(harness.DownloadDirectory),
            Path.GetDirectoryName(Path.GetFullPath(nested.Download!.LocalPath)),
            "download left the target folder");
        Check.Equal(FakeCloudHost.FileContent.LongLength, nested.Download.Bytes, "size given as text by the server");

        var empty = await harness.RunAsync(["3"]);
        Check.Equal<long?>(0, empty.Download?.Bytes, "empty file");
    }

    private static async Task SessionFileAsync()
    {
        using var harness = new SpikeHarness();
        await harness.RunAsync([SpikeHarness.TypedAppleId, FakeAppleAccount.DeviceCode, string.Empty], Password);

        var stored = harness.Store.Load();
        Check.Equal<string?>(FakeAppleAccount.TrustToken, stored.TrustToken, "stored trust token");
        Check.Equal<string?>(harness.Server.Account.TrustedSessionToken, stored.SessionToken, "stored session token");
        Check.That(stored.TrustedAt is not null, "time of the code confirmation not stored");
        Check.That(stored.Cookies.Count >= 28, "cookies were dropped by the cookie jar");
        Check.That(stored.Cookies.Exists(cookie => cookie.Expires is null), "session cookies were not kept");

        if (OperatingSystem.IsWindows())
        {
            var raw = Encoding.Latin1.GetString(File.ReadAllBytes(harness.Store.FilePath));
            Check.That(!raw.Contains(FakeAppleAccount.SecretMarker, StringComparison.Ordinal), "session file is readable");
            Check.That(!raw.Contains("TrustToken", StringComparison.Ordinal), "session file is readable");
        }

        // A file that cannot be decrypted must lead to a fresh sign-in, not to a crash.
        File.WriteAllBytes(harness.Store.FilePath, [1, 2, 3, 4, 5]);
        Check.Equal<string?>(null, harness.Store.Load().SessionToken, "damaged session file");
        Check.That(harness.Store.Delete(), "session file could not be deleted");
        Check.That(!harness.Store.Delete(), "second delete reported a file");
    }
}
