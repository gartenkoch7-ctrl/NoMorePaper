using Microsoft.Extensions.Configuration;

namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>
/// Where the Worker finds the scanner folder (docs/kb/eingaenge.md, "Scanner-Ordner"), from configuration
/// key <c>NoMorePaper:ScannerFolder:Path</c> (env <c>NoMorePaper__ScannerFolder__Path</c>). Empty means the
/// input is off; docker-compose.yml sets <c>/scan</c>, the volume the Samba container shares.
/// </summary>
/// <param name="Path">Absolute path of the folder the scanner writes into.</param>
public sealed record ScannerFolderSettings(string Path)
{
    public const string PathKey = "NoMorePaper:ScannerFolder:Path";

    /// <summary>Whether a path is set at all; checked when the Worker registers its inputs.</summary>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[PathKey]);
    }

    /// <exception cref="InvalidOperationException">No path or a relative one; the message names the key, never the value.</exception>
    public static ScannerFolderSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var path = configuration[PathKey]?.Trim();

        // A relative path would depend on the Worker's working directory and could point into its own files.
        if (string.IsNullOrEmpty(path) || !System.IO.Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"Configuration value '{PathKey}' is invalid.");
        }

        return new ScannerFolderSettings(path);
    }
}
