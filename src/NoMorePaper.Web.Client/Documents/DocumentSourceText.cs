using MudBlazor;

namespace NoMorePaper.Web.Client.Documents;

/// <summary>Text key and symbol for the way a document came in (API words of <c>DocumentSummaryDto.Source</c>). Pure logic.</summary>
public static class DocumentSourceText
{
    /// <returns>The text key, or <c>null</c> for a source a newer server knows and this client does not.</returns>
    public static string? Key(string source) => source switch
    {
        "upload" => "NavUpload",
        "camera" => "NavCamera",
        "scanner-folder" => "InpKindScannerFolder",
        "mail" => "InpKindMail",
        "icloud" => "InpKindICloud",
        _ => null,
    };

    public static string Icon(string source) => source switch
    {
        "upload" => Icons.Material.Outlined.UploadFile,
        "camera" => Icons.Material.Outlined.PhotoCamera,
        "scanner-folder" => Icons.Material.Outlined.Scanner,
        "mail" => Icons.Material.Outlined.Mail,
        "icloud" => Icons.Material.Outlined.Cloud,
        _ => Icons.Material.Outlined.Description,
    };
}
