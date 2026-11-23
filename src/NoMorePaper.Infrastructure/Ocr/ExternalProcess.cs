using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Ocr;

/// <summary>
/// Runs a command-line tool without a shell: arguments go through <see cref="ProcessStartInfo.ArgumentList"/>,
/// so no file name or path can be read as an option or a shell command. Standard input is closed,
/// standard output is discarded or written to a file of the caller (it never reaches the Worker's own log),
/// and only the end of standard error is kept for the caller.
/// </summary>
public sealed class ExternalProcess(TimeProvider time, ILogger<ExternalProcess> logger)
{
    /// <summary>Characters of standard error kept in the result.</summary>
    public const int StdErrTailLength = 2000;

    // A killed tree closes its pipes at once; this only guards against a stuck grandchild.
    private static readonly TimeSpan s_drainAfterKill = TimeSpan.FromSeconds(5);

    /// <summary>Runs the program with the inherited temporary folder.</summary>
    /// <inheritdoc cref="RunAsync(string, IReadOnlyList{string}, string?, TimeSpan, CancellationToken)"/>
    public Task<(int ExitCode, string StdErrTail)> RunAsync(
        string fileName, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken) =>
        RunAsync(fileName, args, tempFolder: null, timeout, cancellationToken);

    /// <param name="fileName">Program name looked up on <c>PATH</c>, e.g. <c>ocrmypdf</c>.</param>
    /// <param name="args">Arguments, each passed as one argument exactly as given.</param>
    /// <param name="tempFolder">
    /// Passed to the program and its children as <c>TMPDIR</c>, so their temporary files land there
    /// (and are removed with it) instead of in the RAM-backed <c>/tmp</c> of the container.
    /// <c>null</c> keeps the inherited value.
    /// </param>
    /// <param name="timeout">After this time the program and all its children are killed.</param>
    /// <returns>Exit code and the last <see cref="StdErrTailLength"/> characters of standard error.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">The program could not be started, e.g. it is not installed.</exception>
    /// <exception cref="TimeoutException">The program ran longer than <paramref name="timeout"/>.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; the program was killed.</exception>
    public Task<(int ExitCode, string StdErrTail)> RunAsync(
        string fileName, IReadOnlyList<string> args, string? tempFolder, TimeSpan timeout, CancellationToken cancellationToken) =>
        RunCoreAsync(fileName, args, tempFolder, Stream.Null, timeout, cancellationToken);

    /// <summary>
    /// Runs the program like <see cref="RunAsync(string, IReadOnlyList{string}, string?, TimeSpan, CancellationToken)"/>
    /// and writes its standard output to <paramref name="outputPath"/> (for tools that answer only there,
    /// e.g. <c>pdffonts</c>); the file is the caller's, in its working folder.
    /// </summary>
    /// <inheritdoc cref="RunAsync(string, IReadOnlyList{string}, string?, TimeSpan, CancellationToken)"/>
    public async Task<(int ExitCode, string StdErrTail)> RunToFileAsync(
        string fileName, IReadOnlyList<string> args, string outputPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await using (output.ConfigureAwait(false))
        {
            return await RunCoreAsync(fileName, args, Path.GetDirectoryName(Path.GetFullPath(outputPath)), output, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the program like <see cref="RunAsync(string, IReadOnlyList{string}, TimeSpan, CancellationToken)"/>
    /// and copies its standard output to <paramref name="output"/> (for short answers, e.g. a page count);
    /// a stream of fixed size makes a longer output fail with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <inheritdoc cref="RunAsync(string, IReadOnlyList{string}, string?, TimeSpan, CancellationToken)"/>
    public Task<(int ExitCode, string StdErrTail)> RunToStreamAsync(
        string fileName, IReadOnlyList<string> args, Stream output, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        return RunCoreAsync(fileName, args, tempFolder: null, output, timeout, cancellationToken);
    }

    private async Task<(int ExitCode, string StdErrTail)> RunCoreAsync(
        string fileName, IReadOnlyList<string> args, string? tempFolder, Stream standardOutput, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = StartInfo(fileName, args, tempFolder) };
        var started = time.GetTimestamp();
        process.Start();
        process.StandardInput.Close();
        var drainOutput = process.StandardOutput.BaseStream.CopyToAsync(standardOutput, CancellationToken.None);
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

    private static ProcessStartInfo StartInfo(string fileName, IReadOnlyList<string> args, string? tempFolder)
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

        if (tempFolder is not null)
        {
            // TMPDIR is read by Python (OCRmyPDF), Tesseract and Ghostscript on Linux, the Worker's platform.
            startInfo.Environment["TMPDIR"] = tempFolder;
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
