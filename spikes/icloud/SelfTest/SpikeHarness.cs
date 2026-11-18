using ICloudSpike.Session;

namespace ICloudSpike.SelfTest;

/// <summary>
/// Runs the real <see cref="SpikeApp"/> against the simulated Apple server with scripted input.
/// Several runs on one harness share server state and session file, like restarts of the program.
/// </summary>
internal sealed class SpikeHarness : IDisposable
{
    /// <summary>Typed with spaces and capitals on purpose: the app must normalise the Apple-ID.</summary>
    public const string TypedAppleId = "  Spike@Example.COM ";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "NoMorePaperSpike-selftest-" + Guid.NewGuid().ToString("N"));

    public SpikeHarness()
    {
        ISecretProtector protector = OperatingSystem.IsWindows() ? new WindowsDataProtector() : new ReversingProtector();
        Store = new SessionStore(Path.Combine(_directory, "session"), protector);
    }

    public FakeAppleServer Server { get; } = new();

    public SessionStore Store { get; }

    public string DownloadDirectory => Path.Combine(_directory, "downloads");

    /// <summary>Console output and HTTP trace of all runs so far.</summary>
    public List<string> Transcript { get; } = [];

    public async Task<SpikeOutcome> RunAsync(string[] answers, params string[] secrets)
    {
        var console = new ScriptedConsole(answers, secrets);
        Server.Calls.Clear();
        var options = new SpikeOptions(Preflight: true, ShowTrace: true, DownloadDirectory);
        var outcome = await new SpikeApp(console, Server, Store, TimeProvider.System, options)
            .RunAsync(CancellationToken.None);
        Transcript.AddRange(console.Transcript);
        Check.Equal(0, console.UnusedAnswers, "scripted answers left over");
        return outcome;
    }

    /// <summary>Password, tokens, cookie values and signed addresses must never be displayed.</summary>
    public void AssertNoSecretShown()
    {
        Check.That(Transcript.Count > 0, "nothing was shown at all");
        foreach (var line in Transcript)
        {
            Check.That(
                !line.Contains(FakeAppleAccount.SecretMarker, StringComparison.Ordinal),
                "a secret was written to console or trace");
        }
    }

    public void Dispose()
    {
        Server.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
