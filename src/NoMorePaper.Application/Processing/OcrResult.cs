namespace NoMorePaper.Application.Processing;

/// <summary>Outcome of <see cref="IOcrEngine.CreateArchiveAsync"/>.</summary>
/// <param name="Text">Text of the archive PDF; empty when it contains none, cut at a fixed length when very long.</param>
/// <param name="PageCount">Pages of the archive PDF.</param>
public sealed record OcrResult(string Text, int PageCount);
