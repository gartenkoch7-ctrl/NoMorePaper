namespace ICloudSpike;

/// <param name="Preflight">Load Apple's sign-in frame before the password step, like a browser.</param>
/// <param name="ShowTrace">Print one line per HTTP call (method, host, path, status).</param>
/// <param name="DownloadDirectory">Where the chosen file is stored.</param>
internal sealed record SpikeOptions(bool Preflight, bool ShowTrace, string DownloadDirectory);
