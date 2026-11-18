namespace NoMorePaper.Server.Tests.Hosting;

/// <summary>Small files that start with the magic bytes of each accepted type.</summary>
internal static class SampleFiles
{
    public static byte[] Pdf() => WithHead("%PDF-1.7\n"u8);

    public static byte[] Jpeg() => WithHead([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46]);

    public static byte[] Png() => WithHead([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]);

    public static byte[] Heic() => WithHead([0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8, 0x00, 0x00, 0x00, 0x00]);

    public static byte[] Text() => "Just some text, not a document."u8.ToArray();

    /// <summary>The head followed by pseudo-random-looking filler, so size and hash are meaningful.</summary>
    public static byte[] WithHead(ReadOnlySpan<byte> head, int totalLength = 5000)
    {
        var bytes = new byte[Math.Max(totalLength, head.Length)];
        head.CopyTo(bytes);
        for (var i = head.Length; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 31) % 251);
        }

        return bytes;
    }
}
