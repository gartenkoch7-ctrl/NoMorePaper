using System.Text.Json;
using ICloudSpike.Http;

namespace ICloudSpike.Drive;

/// <summary>A file or folder in iCloud Drive.</summary>
/// <param name="DriveId">"drivewsid": the id used to list a folder.</param>
/// <param name="DocumentId">"docwsid": the id used to download a file.</param>
/// <param name="Zone">Storage zone, normally "com.apple.CloudDocs".</param>
/// <param name="Name">Name without extension.</param>
/// <param name="Extension">Extension without dot; null for folders and files without one.</param>
/// <param name="Type">"FILE", "FOLDER" or "APP_LIBRARY".</param>
/// <param name="Size">Size in bytes; null for folders.</param>
internal sealed record DriveItem(
    string DriveId, string? DocumentId, string Zone, string Name, string? Extension, string Type, long? Size)
{
    public const string DefaultZone = "com.apple.CloudDocs";

    public bool IsFile => string.Equals(Type, "FILE", StringComparison.OrdinalIgnoreCase);

    public string FileName => string.IsNullOrEmpty(Extension) ? Name : $"{Name}.{Extension}";

    public static DriveItem? Parse(JsonElement element)
    {
        JsonElement? json = element;
        var driveId = json.Child("drivewsid").Text();
        var name = json.Child("name").Text();
        if (driveId is null || name is null)
        {
            return null;
        }

        return new DriveItem(
            driveId,
            json.Child("docwsid").Text(),
            json.Child("zone").Text() ?? DefaultZone,
            name,
            json.Child("extension").Text(),
            json.Child("type").Text() ?? "UNKNOWN",
            json.Child("size").Number());
    }
}
