using ICloudSpike.Terminal;

namespace ICloudSpike.SelfTest;

/// <summary>
/// Runs every check that needs neither network nor Apple-ID: SRP math against published vectors,
/// parsing, error mapping, and the whole dialogue against a simulated Apple server.
/// </summary>
internal sealed class SelfTestRunner
{
    private readonly IUserConsole _console;

    public SelfTestRunner(IUserConsole console) => _console = console;

    public async Task<int> RunAsync()
    {
        IEnumerable<(string Name, Func<Task> Run)> tests =
        [
            .. SrpSelfTests.All,
            .. ParsingSelfTests.All,
            .. FlowSelfTests.All,
            .. FailureSelfTests.All,
        ];

        var failed = 0;
        var total = 0;
        foreach (var (name, run) in tests)
        {
            total++;
            try
            {
                await run();
                _console.WriteLine($"OK      {name}");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failed++;
                _console.WriteLine($"FEHLER  {name}: {exception.Message}");
            }
        }

        _console.WriteLine(string.Empty);
        _console.WriteLine(failed == 0
            ? $"Selbsttest bestanden: {total} Prüfungen, alle ohne Netz und ohne Apple-ID."
            : $"Selbsttest NICHT bestanden: {failed} von {total} Prüfungen fehlgeschlagen.");
        return failed == 0 ? 0 : 1;
    }
}
