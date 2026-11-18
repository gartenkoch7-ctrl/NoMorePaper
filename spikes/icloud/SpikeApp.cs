using System.Globalization;
using ICloudSpike.Auth;
using ICloudSpike.Drive;
using ICloudSpike.Http;
using ICloudSpike.Session;
using ICloudSpike.Terminal;

namespace ICloudSpike;

/// <summary>One run of the spike: get a session, list iCloud Drive, download one file, report.</summary>
internal sealed class SpikeApp
{
    private readonly IUserConsole _console;
    private readonly HttpMessageHandler _handler;
    private readonly SessionStore _store;
    private readonly TimeProvider _clock;
    private readonly SpikeOptions _options;

    public SpikeApp(
        IUserConsole console, HttpMessageHandler handler, SessionStore store, TimeProvider clock, SpikeOptions options)
    {
        _console = console;
        _handler = handler;
        _store = store;
        _clock = clock;
        _options = options;
    }

    public async Task<SpikeOutcome> RunAsync(CancellationToken cancellationToken)
    {
        _console.WriteLine("NoMorePaper – iCloud-Spike. Das Programm liest nur und ändert nichts in iCloud.");
        _console.WriteLine($"Sitzungsdatei (verschlüsselt, außerhalb des Repos): {_store.FilePath}");

        var session = _store.Load();
        using var transport = new ICloudTransport(_handler, Trace);
        session.RestoreCookies(transport.Cookies, _clock.GetUtcNow());

        LoginResult? login = null;
        DriveBrowser? browser = null;
        ICloudFailure? failure = null;
        try
        {
            var channel = new AuthChannel(transport, session);
            var coordinator = new LoginCoordinator(
                _console,
                new AppleAuthClient(channel),
                new VerificationCodeClient(channel),
                new SetupClient(transport, session),
                session,
                _clock,
                () => Save(session, transport));
            login = await coordinator.EnsureLoggedInAsync(_options.Preflight, cancellationToken);
            Save(session, transport);
            ReportAccount(login, session);

            browser = new DriveBrowser(
                _console, new DriveClient(transport, login.Account, session.ClientId), _options.DownloadDirectory);
            await browser.RunAsync(cancellationToken);

            // Service calls may refresh cookies; keep the newest set for the next start.
            Save(session, transport);
        }
        catch (ICloudException exception)
        {
            failure = exception.Failure;
            _console.WriteLine(string.Empty);
            _console.WriteLine("FEHLER: " + FailureMessages.Describe(exception.Failure));
            _console.WriteLine("Technisch: " + exception.Message);
            if (login?.Account is { AdvancedProtection: true } or { WebAccessAllowed: false })
            {
                _console.WriteLine("Hinweis: Für dieses Konto ist „Erweiterter Datenschutz“ aktiv oder der Web-Zugriff gesperrt.");
            }
        }

        var outcome = new SpikeOutcome(login?.Path, browser?.RootItemCount, browser?.Downloaded, failure);
        ReportOutcome(outcome);
        return outcome;
    }

    private void Save(SessionData session, ICloudTransport transport)
    {
        session.CaptureCookies(transport.Cookies);
        _store.Save(session);
    }

    private void Trace(string line)
    {
        if (_options.ShowTrace)
        {
            _console.WriteLine("  [http] " + line);
        }
    }

    private void ReportAccount(LoginResult login, SessionData session)
    {
        var account = login.Account;
        _console.WriteLine(string.Empty);
        _console.WriteLine("Angemeldet. Weg: " + FailureMessages.Describe(login.Path) + ".");
        _console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Konto: Zwei-Faktor-Stufe {account.HsaVersion}, Web-Zugriff erlaubt: {FailureMessages.YesNo(account.WebAccessAllowed)}, "
            + $"Erweiterter Datenschutz: {FailureMessages.YesNo(account.AdvancedProtection)}."));
        if (session.TrustedAt is { } trustedAt)
        {
            var days = (int)(_clock.GetUtcNow() - trustedAt).TotalDays;
            _console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Letzte Bestätigung mit Code: {trustedAt:yyyy-MM-dd} (vor {days} Tagen)."));
        }
    }

    private void ReportOutcome(SpikeOutcome outcome)
    {
        _console.WriteLine(string.Empty);
        _console.WriteLine("Ergebnis für die Checkliste in ADR-0018:");
        _console.WriteLine("  Anmeldung: " + (outcome.Path is { } path ? "ja – " + FailureMessages.Describe(path) : "nein"));
        _console.WriteLine("  Ordner aufgelistet: " + (outcome.RootItemCount is { } count
            ? string.Create(CultureInfo.InvariantCulture, $"ja – {count} Einträge")
            : "nein"));
        _console.WriteLine("  Datei geladen: " + (outcome.Download is null ? "nein" : "ja"));
    }
}
