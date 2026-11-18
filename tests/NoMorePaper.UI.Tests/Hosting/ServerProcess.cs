using System.Collections.Concurrent;
using System.Diagnostics;

namespace NoMorePaper.UI.Tests.Hosting;

/// <summary>
/// Runs the real NoMorePaper.Server (which hosts the WASM client) as a child process, exactly as
/// <c>dotnet run</c> would, so the UI tests see the same static-asset pipeline as a developer.
/// </summary>
public sealed class ServerProcess : IAsyncDisposable
{
    private const int MaxLoggedLines = 300;
    private const string ListeningPrefix = "Now listening on: ";

    private readonly Process _process;
    private readonly ConcurrentQueue<string> _output = new();
    private volatile Uri? _baseAddress;

    private ServerProcess(Process process) => _process = process;

    /// <summary>The address Kestrel actually bound; known once <see cref="WaitUntilReadyAsync"/> returned.</summary>
    public Uri BaseAddress => _baseAddress
        ?? throw new InvalidOperationException("Server address not known yet; call WaitUntilReadyAsync first.");

    /// <summary>
    /// Starts the server on a loopback port chosen by the OS (port 0), so no other process can take
    /// the port between choosing and binding it.
    /// </summary>
    public static ServerProcess Start(IReadOnlyDictionary<string, string> environment)
    {
        var (projectDirectory, assemblyPath) = LocateServer();

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        // Never ship test telemetry anywhere, even if the developer has an exporter configured.
        startInfo.Environment.Remove("OTEL_EXPORTER_OTLP_ENDPOINT");
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        var server = new ServerProcess(process);
        process.OutputDataReceived += (_, e) => server.Record(e.Data);
        process.ErrorDataReceived += (_, e) => server.Record(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return server;
    }

    /// <summary>Waits until the server answers the start page; migrations run before that.</summary>
    public async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Server exited with code {_process.ExitCode} before it was ready.{Environment.NewLine}{Output}");
            }

            if (_baseAddress is not { } address)
            {
                // Kestrel has not reported its port yet (it logs it once migrations are done).
                await Task.Delay(250);
                continue;
            }

            try
            {
                using var response = await client.GetAsync(address);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException)
            {
                // Request timed out while the server is still starting.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Server was not ready after {timeout}.{Environment.NewLine}{Output}");
    }

    /// <summary>The last lines the server wrote, for failure messages.</summary>
    public string Output => string.Join(Environment.NewLine, _output);

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private void Record(string? line)
    {
        if (line is null)
        {
            return;
        }

        var listeningAt = line.IndexOf(ListeningPrefix, StringComparison.Ordinal);
        if (listeningAt >= 0 && _baseAddress is null)
        {
            _baseAddress = new Uri(line[(listeningAt + ListeningPrefix.Length)..].Trim());
        }

        _output.Enqueue(line);
        while (_output.Count > MaxLoggedLines && _output.TryDequeue(out _))
        {
        }
    }

    // The server build output sits next to the test output: same configuration, same TFM.
    private static (string ProjectDirectory, string AssemblyPath) LocateServer()
    {
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent?.Name
            ?? throw new InvalidOperationException("Unexpected test output layout.");

        var root = testOutput;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "NoMorePaper.slnx")))
        {
            root = root.Parent;
        }

        if (root is null)
        {
            throw new InvalidOperationException("Repository root (NoMorePaper.slnx) not found.");
        }

        var projectDirectory = Path.Combine(root.FullName, "src", "NoMorePaper.Server");
        var assemblyPath = Path.Combine(
            projectDirectory, "bin", configuration, targetFramework, "NoMorePaper.Server.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("Server build output not found. Build the solution first.", assemblyPath);
        }

        return (projectDirectory, assemblyPath);
    }
}
