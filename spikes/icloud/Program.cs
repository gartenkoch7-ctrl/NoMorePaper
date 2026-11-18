using System.Text;
using ICloudSpike;
using ICloudSpike.Http;
using ICloudSpike.SelfTest;
using ICloudSpike.Session;
using ICloudSpike.Terminal;

Console.OutputEncoding = Encoding.UTF8;
var console = new SystemConsole();

if (args.Contains("--help"))
{
    console.WriteLine("Aufruf: dotnet run --project spikes/icloud [-- Option]");
    console.WriteLine("  (ohne Option)   anmelden, iCloud Drive auflisten, eine Datei laden");
    console.WriteLine("  --selftest      Prüfungen ohne Netz und ohne Apple-ID");
    console.WriteLine("  --logout        gespeicherte Sitzungsdatei löschen");
    console.WriteLine("  --no-preflight  Anmelde-Rahmen vor der Passwortprüfung nicht laden");
    console.WriteLine("  --quiet         HTTP-Protokollzeilen nicht anzeigen");
    return 0;
}

if (args.Contains("--selftest"))
{
    return await new SelfTestRunner(console).RunAsync();
}

if (!OperatingSystem.IsWindows())
{
    // The session file is encrypted with Windows DPAPI; storing it in clear is not an option.
    console.WriteLine("Der Spike läuft nur unter Windows (Sitzungsdatei wird mit Windows-Mitteln verschlüsselt).");
    return 2;
}

// %LOCALAPPDATA%\NoMorePaperSpike: belongs to the Windows user and lies outside the repository.
var sessionDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoMorePaperSpike");
var store = new SessionStore(sessionDirectory, new WindowsDataProtector());
if (args.Contains("--logout"))
{
    console.WriteLine(store.Delete() ? "Sitzungsdatei gelöscht." : "Es war keine Sitzungsdatei vorhanden.");
    return 0;
}

if (Console.IsInputRedirected)
{
    // Apple-ID, password and code are typed by the user; nothing is read from files or pipes.
    console.WriteLine("Bitte in einem normalen Terminalfenster starten (Eingaben werden von Hand getippt).");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var handler = ICloudTransport.CreateNetworkHandler();
var options = new SpikeOptions(
    Preflight: !args.Contains("--no-preflight"),
    ShowTrace: !args.Contains("--quiet"),
    DownloadDirectory: Path.Combine(Path.GetTempPath(), "NoMorePaperSpike"));
try
{
    var outcome = await new SpikeApp(console, handler, store, TimeProvider.System, options).RunAsync(cancellation.Token);
    return outcome.ExitCode;
}
catch (OperationCanceledException)
{
    console.WriteLine("Abgebrochen.");
    return 1;
}
