using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Infrastructure.Ocr;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Ocr;

/// <summary>Runs programs every test machine has: <c>dotnet</c>, plus the shell and a sleeper of the OS.</summary>
public sealed class ExternalProcessTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(1);
    private readonly ExternalProcess _process = new(TimeProvider.System, NullLogger<ExternalProcess>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_ReturnsExitCodeZero_ForASuccessfulProgram()
    {
        var (exitCode, stdErrTail) = await _process.RunAsync("dotnet", ["--version"], s_timeout, Ct);

        Assert.Equal(0, exitCode);
        Assert.Empty(stdErrTail);
    }

    [Fact]
    public async Task Run_ReturnsTheExitCodeAndStandardError_OfAFailingProgram()
    {
        var (exitCode, stdErrTail) = await _process.RunAsync("dotnet", ["no-such-command-for-nomorepaper"], s_timeout, Ct);

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(stdErrTail);
    }

    [Fact]
    public async Task Run_KeepsOnlyTheEndOfStandardError()
    {
        // 500 lines of "0123456789" (6000 characters and more) and a final marker, all on standard error.
        var (fileName, args) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "(for /L %i in (1,1,500) do @echo 0123456789 1>&2) & echo END 1>&2" })
            : ("sh", new[] { "-c", "for i in $(seq 1 500); do echo 0123456789 >&2; done; echo END >&2" });

        var (exitCode, stdErrTail) = await _process.RunAsync(fileName, args, s_timeout, Ct);

        Assert.Equal(0, exitCode);
        Assert.Equal(ExternalProcess.StdErrTailLength, stdErrTail.Length);
        Assert.EndsWith("END", stdErrTail.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_PassesEachArgumentLiterally_WithoutAShell()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Needs a POSIX shell to echo the argument back.");
        const string argument = "a b; echo \"injected\" $HOME `id` | cat";

        var (_, stdErrTail) = await _process.RunAsync("sh", ["-c", "printf '%s' \"$1\" >&2", "sh", argument], s_timeout, Ct);

        Assert.Equal(argument, stdErrTail);
    }

    [Fact]
    public async Task Run_PassesTheTemporaryFolder_AsTmpdir()
    {
        var folder = Path.Combine(Path.GetTempPath(), "nomorepaper-tmpdir-test");
        var (fileName, args) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "echo %TMPDIR% 1>&2" })
            : ("sh", new[] { "-c", "printf '%s' \"$TMPDIR\" >&2" });

        var (exitCode, stdErrTail) = await _process.RunAsync(fileName, args, folder, s_timeout, Ct);

        Assert.Equal(0, exitCode);
        Assert.Equal(folder, stdErrTail.TrimEnd());
    }

    [Fact]
    public async Task Run_KillsTheProgram_WhenItExceedsTheTimeout()
    {
        var (fileName, args) = Sleeper();
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => _process.RunAsync(fileName, args, TimeSpan.FromMilliseconds(500), Ct));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"Took {watch.Elapsed}");
    }

    [Fact]
    public async Task Run_KillsTheProgram_WhenCancelled()
    {
        var (fileName, args) = Sleeper();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _process.RunAsync(fileName, args, s_timeout, cancellation.Token));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"Took {watch.Elapsed}");
    }

    [Fact]
    public async Task Run_Fails_WhenTheProgramIsNotInstalled() =>
        await Assert.ThrowsAsync<Win32Exception>(() => _process.RunAsync("nomorepaper-no-such-tool", [], s_timeout, Ct));

    [Fact]
    public async Task RunToStream_KeepsAShortAnswer_AndFailsWhenItOverflowsAStreamOfFixedSize()
    {
        var buffer = new byte[64];
        using var answer = new MemoryStream(buffer, writable: true);

        var (exitCode, _) = await _process.RunToStreamAsync("dotnet", ["--version"], answer, s_timeout, Ct);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+", System.Text.Encoding.ASCII.GetString(buffer, 0, (int)answer.Position));
        using var tooSmall = new MemoryStream(new byte[2], writable: true);
        await Assert.ThrowsAsync<NotSupportedException>(() => _process.RunToStreamAsync("dotnet", ["--version"], tooSmall, s_timeout, Ct));
    }

    // Runs for 60 s unless killed.
    private static (string FileName, string[] Args) Sleeper() =>
        OperatingSystem.IsWindows() ? ("ping", ["-n", "60", "127.0.0.1"]) : ("sleep", ["60"]);
}
