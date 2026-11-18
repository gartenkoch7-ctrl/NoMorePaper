namespace NoMorePaper.Server.Documents;

/// <summary>
/// Recognizes the accepted file types by their first bytes, never by the name or the type the
/// uploader claims. Pure logic.
/// </summary>
internal static class FileSignature
{
    /// <summary>Bytes of a file that <see cref="DetectMediaType"/> needs.</summary>
    public const int HeadLength = 16;

    private static readonly byte[] s_pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] s_jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] s_png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[][] s_heicBrands =
        ["heic"u8.ToArray(), "heix"u8.ToArray(), "mif1"u8.ToArray(), "heif"u8.ToArray()];

    /// <returns>The media type, or <c>null</c> if the file is none of PDF, JPEG, PNG, HEIC.</returns>
    public static string? DetectMediaType(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(s_pdf))
        {
            return "application/pdf";
        }

        if (head.StartsWith(s_jpeg))
        {
            return "image/jpeg";
        }

        if (head.StartsWith(s_png))
        {
            return "image/png";
        }

        // ISO base media: box size (4 bytes), "ftyp", then the major brand.
        if (head.Length >= 12 && head.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = head.Slice(8, 4);
            foreach (var known in s_heicBrands)
            {
                if (brand.SequenceEqual(known))
                {
                    return "image/heic";
                }
            }
        }

        return null;
    }
}
