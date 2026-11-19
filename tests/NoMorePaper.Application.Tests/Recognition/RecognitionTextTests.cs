using NoMorePaper.Application.Recognition;
using Xunit;

namespace NoMorePaper.Application.Tests.Recognition;

public sealed class RecognitionTextTests
{
    [Fact]
    public void Compact_CollapsesPaddingRulesAndBlankLines()
    {
        var text = "  Rechnung\r\nMenge      Preis\t\t\tSumme\n----------\n\n\n\nGesamt ........ 84,20  ";

        Assert.Equal("Rechnung\nMenge  Preis  Summe\n---\n\nGesamt --- 84,20", RecognitionText.Compact(text));
    }

    [Fact]
    public void Shorten_KeepsAShortTextWhole()
    {
        var text = string.Join('\n', Enumerable.Repeat("Zeile mit vierzig Zeichen Inhalt drin.", 100));

        Assert.Equal(text, RecognitionText.Shorten(text));
    }

    [Fact]
    public void Shorten_KeepsHeadAndTail_CutAtLineEnds()
    {
        var lines = Enumerable.Range(0, 300).Select(i => $"Zeile {i:000} mit etwas Text darin.").ToList();
        var text = string.Join('\n', lines);

        var shortened = RecognitionText.Shorten(text);

        Assert.StartsWith(lines[0] + "\n", shortened, StringComparison.Ordinal);
        Assert.EndsWith("\n" + lines[^1], shortened, StringComparison.Ordinal);
        Assert.Contains(RecognitionText.Marker, shortened, StringComparison.Ordinal);
        var (head, tail) = (shortened.Split(RecognitionText.Marker)[0], shortened.Split(RecognitionText.Marker)[1]);
        Assert.InRange(head.Length, RecognitionText.HeadLength / 2, RecognitionText.HeadLength);
        Assert.InRange(tail.Length, RecognitionText.TailLength / 2, RecognitionText.TailLength);
        Assert.All(head.Split('\n').Concat(tail.Split('\n')), line => Assert.Contains(line, lines));
    }

    [Fact]
    public void Shorten_NeverSplitsAnEmoji()
    {
        var text = new string('a', RecognitionText.HeadLength - 1) + "😀" + new string('b', 2000) + "😀" + new string('z', RecognitionText.TailLength - 1);

        var shortened = RecognitionText.Shorten(text);

        Assert.DoesNotContain(shortened, c => char.IsSurrogate(c));
        Assert.StartsWith(new string('a', RecognitionText.HeadLength - 1) + RecognitionText.Marker, shortened, StringComparison.Ordinal);
    }

    [Fact]
    public void Shorten_CutsInsideALine_WhenTheTextHasNoLineEnds()
    {
        var text = new string('a', 5000) + new string('z', 1000);

        var shortened = RecognitionText.Shorten(text);

        Assert.Equal(
            new string('a', RecognitionText.HeadLength) + RecognitionText.Marker + new string('z', RecognitionText.TailLength),
            shortened);
    }
}
