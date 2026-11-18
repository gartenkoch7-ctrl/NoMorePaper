using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ICloudSpike.Auth;
using ICloudSpike.Http;

namespace ICloudSpike.Drive;

/// <summary>Read-only access to iCloud Drive: list a folder, download a file.</summary>
internal sealed class DriveClient
{
    public const string RootId = "FOLDER::com.apple.CloudDocs::root";

    private readonly ICloudTransport _transport;
    private readonly string _driveUrl;
    private readonly string _documentsUrl;
    private readonly Dictionary<string, string> _query;

    public DriveClient(ICloudTransport transport, AccountState account, string clientId)
    {
        if (account.DriveUrl is null || account.DocumentsUrl is null)
        {
            // Without these addresses there is nothing to call. Typical cause: Advanced Data
            // Protection with web access switched off.
            throw new ICloudException(
                ICloudFailure.WebAccessBlocked, "iCloud did not offer the Drive web services for this account.");
        }

        _transport = transport;
        _driveUrl = account.DriveUrl;
        _documentsUrl = account.DocumentsUrl;
        _query = SetupClient.ServiceQuery(clientId, account.Dsid);
    }

    public async Task<IReadOnlyList<DriveItem>> ListFolderAsync(string driveId, CancellationToken cancellationToken)
    {
        var body = new JsonArray(new JsonObject { ["drivewsid"] = driveId, ["partialData"] = false });
        var uri = ApiRequest.BuildUri(_driveUrl + "/retrieveItemDetailsInFolders", _query);
        var response = await _transport.SendAsync(
            new ApiRequest(HttpMethod.Post, uri)
            {
                Body = body.ToJsonString(),
                ContentType = ApiRequest.PlainTextContent,
            },
            cancellationToken);
        EnsureSuccess("retrieveItemDetailsInFolders", response);

        // The answer is an array with one entry per requested folder; its "items" are the children.
        var folder = response.Json.Items().FirstOrDefault();
        var items = ((System.Text.Json.JsonElement?)folder).Child("items");
        if (items is null)
        {
            throw new ICloudException(
                ICloudFailure.ProtocolChanged, "retrieveItemDetailsInFolders: the answer has no item list.", response.Status);
        }

        return [.. items.Items().Select(DriveItem.Parse).OfType<DriveItem>()];
    }

    /// <summary>Downloads the original file into <paramref name="directory"/> and returns size and hash.</summary>
    public async Task<DownloadedFile> DownloadAsync(DriveItem item, string directory, CancellationToken cancellationToken)
    {
        if (!item.IsFile || item.DocumentId is null)
        {
            throw new ICloudException(ICloudFailure.Unexpected, "download: the chosen entry is not a file.");
        }

        Directory.CreateDirectory(directory);
        var localPath = Path.Combine(directory, SafeLocalName(item.FileName));
        await using (var target = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            // An empty file has no content address; creating the empty local file is the download.
            if (item.Size != 0)
            {
                var contentUri = await ResolveContentUriAsync(item, cancellationToken);
                await _transport.DownloadAsync(contentUri, target, cancellationToken);
            }
        }

        await using var written = File.OpenRead(localPath);
        var hash = await SHA256.HashDataAsync(written, cancellationToken);
        return new DownloadedFile(localPath, written.Length, Convert.ToHexStringLower(hash));
    }

    /// <summary>Step one of a download: iCloud answers with a short-lived, pre-signed content address.</summary>
    private async Task<Uri> ResolveContentUriAsync(DriveItem item, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(_query) { ["document_id"] = item.DocumentId! };
        var uri = ApiRequest.BuildUri(
            $"{_documentsUrl}/ws/{Uri.EscapeDataString(item.Zone)}/download/by_id", query);
        var response = await _transport.SendAsync(new ApiRequest(HttpMethod.Get, uri), cancellationToken);
        EnsureSuccess("download/by_id", response);

        // "package_token" is used for bundles (e.g. Pages documents), "data_token" for plain files.
        var address = response.Json.Child("data_token").Child("url").Text()
            ?? response.Json.Child("package_token").Child("url").Text();
        if (address is null || !Uri.TryCreate(address, UriKind.Absolute, out var contentUri)
            || contentUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ICloudException(
                ICloudFailure.ProtocolChanged, "download/by_id: the answer has no usable content address.", response.Status);
        }

        return contentUri;
    }

    private static void EnsureSuccess(string step, ApiResponse response)
    {
        if (response.Status == 401)
        {
            throw ResponseErrors.Create(step, response, ICloudFailure.SessionExpired);
        }

        if (!response.IsSuccess)
        {
            throw ResponseErrors.Create(step, response);
        }
    }

    /// <summary>The name comes from the server; it must not be able to leave the target folder.</summary>
    private static string SafeLocalName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Concat(name.Select(character => invalid.Contains(character) ? '_' : character))
            .Trim(' ', '.');
        return cleaned.Length == 0 ? "download.bin" : cleaned;
    }
}
