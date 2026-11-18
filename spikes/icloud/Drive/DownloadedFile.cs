namespace ICloudSpike.Drive;

/// <summary>A file fetched from iCloud Drive to local disk.</summary>
internal sealed record DownloadedFile(string LocalPath, long Bytes, string Sha256);
