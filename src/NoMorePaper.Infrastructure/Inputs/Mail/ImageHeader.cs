using System.Buffers.Binary;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// Reads the pixel size of a JPEG or PNG from its header, without decoding the image: the mail input only
/// needs to tell logos and tracking pixels from photographed pages. Pure logic.
/// </summary>
public static class ImageHeader
{
    private static readonly byte[] s_png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <returns>Width and height, or <c>null</c> for anything else or a damaged header.</returns>
    public static (int Width, int Height)? Size(ReadOnlySpan<byte> image)
    {
        if (image.Length >= 24 && image.StartsWith(s_png) && image.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            var width = BinaryPrimitives.ReadInt32BigEndian(image.Slice(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(image.Slice(20, 4));
            return width > 0 && height > 0 ? (width, height) : null;
        }

        return image.Length >= 4 && image[0] == 0xFF && image[1] == 0xD8 ? JpegSize(image) : null;
    }

    // Walks the segments up to the first start of frame (SOF0–SOF15 except DHT, JPG and DAC).
    private static (int Width, int Height)? JpegSize(ReadOnlySpan<byte> image)
    {
        var position = 2;
        while (position + 4 <= image.Length)
        {
            if (image[position] != 0xFF)
            {
                return null;
            }

            var marker = image[position + 1];
            if (marker == 0xFF)
            {
                // Fill byte before a marker.
                position++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                // Markers without a segment.
                position += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(image.Slice(position + 2, 2));
            if (length < 2)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (position + 9 > image.Length)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(image.Slice(position + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(image.Slice(position + 7, 2));
                return width > 0 && height > 0 ? (width, height) : null;
            }

            if (marker is 0xD9 or 0xDA)
            {
                // End of image or start of scan before any frame header.
                return null;
            }

            position += 2 + length;
        }

        return null;
    }
}
