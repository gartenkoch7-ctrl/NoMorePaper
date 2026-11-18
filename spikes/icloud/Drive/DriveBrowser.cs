using System.Globalization;
using ICloudSpike.Terminal;

namespace ICloudSpike.Drive;

/// <summary>Console dialogue: show a folder, open sub-folders, download one chosen file.</summary>
internal sealed class DriveBrowser
{
    private const int MaxInvalidInputs = 5;

    private readonly IUserConsole _console;
    private readonly DriveClient _drive;
    private readonly string _downloadDirectory;

    public DriveBrowser(IUserConsole console, DriveClient drive, string downloadDirectory)
    {
        _console = console;
        _drive = drive;
        _downloadDirectory = downloadDirectory;
    }

    /// <summary>Number of entries in the root folder; null until the listing succeeded.</summary>
    public int? RootItemCount { get; private set; }

    /// <summary>The downloaded file; null if none was chosen or the download failed.</summary>
    public DownloadedFile? Downloaded { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var folderId = DriveClient.RootId;
        var folderName = "iCloud Drive";
        while (true)
        {
            var items = await _drive.ListFolderAsync(folderId, cancellationToken);
            RootItemCount ??= items.Count;
            Show(folderName, items);
            var chosen = Choose(items);
            if (chosen is null)
            {
                return;
            }

            if (chosen.IsFile)
            {
                await DownloadAsync(chosen, cancellationToken);
                return;
            }

            folderId = chosen.DriveId;
            folderName = chosen.FileName;
        }
    }

    private void Show(string folderName, IReadOnlyList<DriveItem> items)
    {
        _console.WriteLine(string.Empty);
        _console.WriteLine($"Ordner „{folderName}“: {items.Count} Einträge.");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var kind = item.IsFile ? "Datei" : "Ordner";
            var size = item.Size is { } bytes ? string.Create(CultureInfo.InvariantCulture, $", {bytes} Bytes") : string.Empty;
            _console.WriteLine($"  {index + 1}: {kind}: {item.FileName}{size}");
        }
    }

    private DriveItem? Choose(IReadOnlyList<DriveItem> items)
    {
        if (items.Count == 0)
        {
            _console.WriteLine("Der Ordner ist leer.");
            return null;
        }

        for (var attempt = 0; attempt < MaxInvalidInputs; attempt++)
        {
            var entry = _console.ReadLine("Nummer eingeben (Datei wird geladen, Ordner geöffnet), leer = Ende: ");
            if (entry.Length == 0)
            {
                return null;
            }

            if (int.TryParse(entry, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number >= 1 && number <= items.Count)
            {
                return items[number - 1];
            }

            _console.WriteLine("Keine gültige Nummer.");
        }

        return null;
    }

    private async Task DownloadAsync(DriveItem item, CancellationToken cancellationToken)
    {
        var file = await _drive.DownloadAsync(item, _downloadDirectory, cancellationToken);
        Downloaded = file;
        _console.WriteLine(string.Empty);
        _console.WriteLine($"Datei geladen nach: {file.LocalPath}");
        _console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Größe: {file.Bytes} Bytes, SHA-256: {file.Sha256}"));
        if (item.Size is { } expected && expected != file.Bytes)
        {
            _console.WriteLine(string.Create(
                CultureInfo.InvariantCulture, $"ACHTUNG: iCloud nennt {expected} Bytes – die Größe stimmt nicht überein."));
        }
    }
}
