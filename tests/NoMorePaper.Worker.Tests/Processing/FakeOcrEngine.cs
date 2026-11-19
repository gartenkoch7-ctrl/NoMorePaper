using NoMorePaper.Application.Processing;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Stands in for OCRmyPDF: by default writes a small "PDF" and returns a fixed text.</summary>
internal sealed class FakeOcrEngine : IOcrEngine
{
    public const string Text = "Rechnung Stromanbieter Januar";

    /// <summary>What a call does; receives input path, output path and the cancellation token.</summary>
    public Func<string, string, CancellationToken, Task<OcrResult>> Behavior { get; set; } = WriteArchiveAsync;

    public int Calls { get; private set; }

    /// <summary>Input path of the last call, and whether the working copy existed then.</summary>
    public (string Path, bool Existed)? LastInput { get; private set; }

    /// <summary>Completed when a call has started.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<OcrResult> CreateArchiveAsync(string inputPath, string mediaType, string outputPdfPath, CancellationToken cancellationToken)
    {
        Calls++;
        LastInput = (inputPath, File.Exists(inputPath));
        Started.TrySetResult();
        return Behavior(inputPath, outputPdfPath, cancellationToken);
    }

    public static Func<string, string, CancellationToken, Task<OcrResult>> Throws(Exception exception) =>
        (_, _, _) => Task.FromException<OcrResult>(exception);

    /// <summary>Runs until cancelled, like a long OCR job during shutdown.</summary>
    public static async Task<OcrResult> WaitForCancellationAsync(string input, string output, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Not reached.");
    }

    public static Task<OcrResult> WriteArchiveAsync(string input, string output, CancellationToken cancellationToken) =>
        Recognizes(Text)(input, output, cancellationToken);

    /// <summary>Writes the archive and returns <paramref name="text"/> as the recognized text.</summary>
    public static Func<string, string, CancellationToken, Task<OcrResult>> Recognizes(string text) =>
        async (_, output, cancellationToken) =>
        {
            await File.WriteAllTextAsync(output, "%PDF-1.7 archive", cancellationToken);
            return new OcrResult(text, 1);
        };
}
