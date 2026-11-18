using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Ocr;

/// <summary>
/// Runs a command-line tool without a shell: arguments go through <see cref="ProcessStartInfo.ArgumentList"/>,
/// so no file name or path can be read as an option or a shell command. Standard input is closed,
/// standard output is discarded (it never reaches the Worker's own log), and only the end of
/// standard error is kept for the caller.
/// </summary>
public sealed class ExternalProcess(TimeProvider time, ILogger<ExternalProcess> logger)
{
    /// <summary>Characters of standard error kept in the result.</summary>
    public const int StdErrTailLength = 2000;

    // A killed tree closes its pipes at once; this only guards against a stuck grandchild.
    private static readonly TimeSpan s_drainAfterKill = TimeSpan.FromSeconds(5);

    /// <param name="fileName">Program name looked up on <c>PATH</c>, e.g. <c>ocrmypdf</c>.</param>
    /// <param name="args">Arguments, each passed as one argument exactly as given.</param>
    /// <param name="timeout">After this time the program and all its children are killed.</param>
    /// <returns>Exit code and the last <see cref="StdErrTailLength"/> characters of standard error.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">The program could not be started, e.g. it is not installed.</exception>
    /// <exception cref="TimeoutException">The program ran longer than <paramref name="timeout"/>.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; the program was killed.</exception>
    public async Task<(int ExitCode, string StdErrTail)> RunAsync(
        string fileName, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = StartInfo(fileName, args) };
        var started = time.GetTimestamp();
        process.Start();
        process.StandardInput.Close();
        var drainOutput = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None);
        var readErrors = ReadTailAsync(process.StandardError);

        using var timeoutSource = new CancellationTokenSource(timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            // Waiting for the pipes too: a tool's output is complete only when they are closed.
            await Task.WhenAll(process.WaitForExitAsync(linked.Token), drainOutput.WaitAsync(linked.Token), readErrors.WaitAsync(linked.Token))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await KillAsync(process, drainOutput, readErrors).ConfigureAwait(false);
            if (exception is not OperationCanceledException)
            {
                throw;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                logger.ToolCancelled(fileName);
                throw;
            }

            logger.ToolTimedOut(fileName, timeout.TotalSeconds);
            throw new TimeoutException($"{fileName} did not finish within {timeout.TotalSeconds} s.");
        }

        var exitCode = process.ExitCode;
        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.ToolExited(fileName, exitCode, elapsedMs);
        return (exitCode, await readErrors.ConfigureAwait(false));
    }

    private static ProcessStartInfo StartInfo(string fileName, IReadOnlyList<string> args)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }

    private async Task KillAsync(Process process, Task drainOutput, Task<string> readErrors)
    {
        try
        {
            // The whole tree: ocrmypdf starts tesseract and Ghostscript as children.
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Exited on its own in the meantime.
        }

        try
        {
            await Task.WhenAll(process.WaitForExitAsync(CancellationToken.None), drainOutput, readErrors)
                .WaitAsync(s_drainAfterKill, time)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or ObjectDisposedException)
        {
            // The caller reports the timeout or cancellation; leftover pipes do not change that.
        }
    }

    private static async Task<string> ReadTailAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        var tail = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
        {
            tail.Append(buffer, 0, read);
            if (tail.Length > 2 * StdErrTailLength)
            {
                tail.Remove(0, tail.Length - StdErrTailLength);
            }
        }

        return tail.Length > StdErrTailLength ? tail.ToString(tail.Length - StdErrTailLength, StdErrTailLength) : tail.ToString();
    }
}
