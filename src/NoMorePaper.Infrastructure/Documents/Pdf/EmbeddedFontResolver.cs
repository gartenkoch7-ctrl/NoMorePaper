using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace NoMorePaper.Infrastructure.Documents.Pdf;

/// <summary>
/// Gives PDFsharp DejaVu Sans from the assembly's resources for every font it asks for: the Linux
/// containers have no fonts, and a PDF must look the same everywhere. DejaVu covers German umlauts, ß
/// and the euro sign.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    /// <summary>The family the writers ask for.</summary>
    public const string Family = "DejaVu Sans";

    private const string Regular = "DejaVuSans";
    private const string Bold = "DejaVuSans-Bold";
    private const string ResourcePrefix = "NoMorePaper.Fonts.";

    private readonly ConcurrentDictionary<string, byte[]> _fonts = new(StringComparer.Ordinal);

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? Bold : Regular, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[]? GetFont(string faceName) => _fonts.GetOrAdd(faceName, Load);

    private static byte[] Load(string faceName)
    {
        var name = ResourcePrefix + (faceName == Bold ? Bold : Regular) + ".ttf";
        using var stream = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Font resource {name} is missing from the build.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
