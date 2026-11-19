using System.Text;
using System.Text.RegularExpressions;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>A small reader for the multipart/form-data body a mocked route received (names and raw bytes).</summary>
internal sealed partial class MultipartForm(IReadOnlyList<MultipartForm.Part> parts)
{
    public IReadOnlyList<Part> Parts { get; } = parts;

    public string? Value(string name) =>
        Parts.FirstOrDefault(part => part.Name == name) is { } part ? Encoding.UTF8.GetString(part.Body) : null;

    public static MultipartForm Parse(string contentType, byte[] body)
    {
        var boundary = Encoding.ASCII.GetBytes("--" + BoundaryOf().Match(contentType).Groups["b"].Value.Trim('"'));
        var parts = new List<Part>();
        var at = IndexOf(body, boundary, 0);
        while (at >= 0)
        {
            var start = at + boundary.Length + 2; // CRLF after the boundary ("--" after the last one)
            var next = IndexOf(body, boundary, start);
            if (next < 0)
            {
                break;
            }

            var headerEnd = IndexOf(body, "\r\n\r\n"u8.ToArray(), start);
            var headers = Encoding.ASCII.GetString(body, start, headerEnd - start);
            var content = body[(headerEnd + 4)..(next - 2)]; // CRLF before the next boundary
            parts.Add(new Part(NameOf().Match(headers).Groups["n"].Value, content));
            at = next;
        }

        return new MultipartForm(parts);
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from) =>
        haystack.AsSpan(from).IndexOf(needle) is var found and >= 0 ? from + found : -1;

    [GeneratedRegex("boundary=(?<b>[^;]+)")]
    private static partial Regex BoundaryOf();

    [GeneratedRegex("(?<![a-z*])name=\"?(?<n>[^\";\r\n]+)\"?")]
    private static partial Regex NameOf();

    public sealed record Part(string Name, byte[] Body);
}
