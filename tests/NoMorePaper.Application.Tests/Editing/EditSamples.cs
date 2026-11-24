using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>Documents, files and page images for the editing tests.</summary>
internal static class EditSamples
{
    public static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Starts like a JPEG (JFIF); the use cases only read the signature.</summary>
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1];

    public static DocumentFile File(DocumentFileKind kind, char sha) => new(kind, kind + ".pdf", "application/pdf", 10, new string(sha, 64));

    /// <summary>Makes the document done, with original and archive (and an edited version if asked).</summary>
    public static Document Done(this Document document, bool edited = false)
    {
        document.AttachFile(File(DocumentFileKind.Original, 'a'));
        document.StartProcessing();
        document.Complete("Rechnung");
        document.AttachFile(File(DocumentFileKind.Archive, 'b'));
        if (edited)
        {
            document.ReplaceFile(File(DocumentFileKind.Edited, 'c'), Now.AddDays(-1));
        }

        return document;
    }

    public static EditPageImage Image(int page, byte[]? bytes = null)
    {
        var content = bytes ?? Jpeg;
        return new EditPageImage(page, content.Length, () => new MemoryStream(content));
    }
}
