using System.Globalization;
using System.Resources;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Localization;
using NoMorePaper.Web.Client.Viewer;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Viewer;

/// <summary>The history of a document: newest first, only the arrival has a time and a source.</summary>
public sealed class HistoryTimelineTests
{
    private static readonly string[] _knownSources = ["upload", "camera", "scanner-folder", "mail", "icloud"];
    private static readonly DateTimeOffset _at = new(2026, 11, 2, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Received_IsAlwaysLast_WithTimeAndSource()
    {
        var entries = HistoryTimeline.Build(_at, DocumentProcessingState.Done, DocumentRecognitionState.Confirmed, "mail");

        var last = entries[^1];
        Assert.Equal((HistoryEntryKind.Received, _at, "HistorySourceMail"), (last.Kind, last.At, last.SourceKey));
    }

    [Theory]
    [InlineData(DocumentProcessingState.Queued, HistoryEntryKind.Queued)]
    [InlineData(DocumentProcessingState.Processing, HistoryEntryKind.Processing)]
    [InlineData(DocumentProcessingState.Done, HistoryEntryKind.TextRecognized)]
    [InlineData(DocumentProcessingState.Failed, HistoryEntryKind.Failed)]
    public void TheState_BecomesTheMiddleEntry(DocumentProcessingState state, HistoryEntryKind expected)
    {
        var entries = HistoryTimeline.Build(_at, state, null, "upload");

        Assert.Equal([expected, HistoryEntryKind.Received], entries.Select(entry => entry.Kind));
    }

    [Theory]
    [InlineData(DocumentRecognitionState.Automatic, HistoryEntryKind.FieldsRecognized)]
    [InlineData(DocumentRecognitionState.Confirmed, HistoryEntryKind.FieldsConfirmed)]
    public void Recognition_IsTheNewestEntry(DocumentRecognitionState recognition, HistoryEntryKind expected)
    {
        var entries = HistoryTimeline.Build(_at, DocumentProcessingState.Done, recognition, "upload");

        Assert.Equal(expected, entries[0].Kind);
        Assert.Equal(3, entries.Count);
    }

    [Theory]
    [InlineData(DocumentRecognitionState.None)]
    [InlineData(null)]
    public void WithoutRecognition_ThereIsNoFieldsEntry(DocumentRecognitionState? recognition)
    {
        var entries = HistoryTimeline.Build(_at, DocumentProcessingState.Done, recognition, "upload");

        Assert.Equal([HistoryEntryKind.TextRecognized, HistoryEntryKind.Received], entries.Select(entry => entry.Kind));
    }

    [Fact]
    public void ConfirmingWhileTheOcrRuns_ShowsBothEntries()
    {
        var entries = HistoryTimeline.Build(_at, DocumentProcessingState.Processing, DocumentRecognitionState.Confirmed, "upload");

        Assert.Equal(
            [HistoryEntryKind.FieldsConfirmed, HistoryEntryKind.Processing, HistoryEntryKind.Received],
            entries.Select(entry => entry.Kind));
    }

    [Theory]
    [InlineData("upload", "HistorySourceUpload")]
    [InlineData("camera", "HistorySourceCamera")]
    [InlineData("scanner-folder", "HistorySourceScannerFolder")]
    [InlineData("mail", "HistorySourceMail")]
    [InlineData("icloud", "HistorySourceICloud")]
    [InlineData("fax", null)]
    [InlineData(null, null)]
    public void SourceKey_NamesKnownSources_AndNothingElse(string? source, string? expected)
    {
        Assert.Equal(expected, HistoryTimeline.SourceKey(source));
    }

    [Fact]
    public void OnlyTheArrival_HasATime()
    {
        var entries = HistoryTimeline.Build(_at, DocumentProcessingState.Done, DocumentRecognitionState.Automatic, "upload");

        Assert.Equal([HistoryEntryKind.Received], entries.Where(entry => entry.At is not null).Select(entry => entry.Kind));
    }

    [Fact]
    public void TitleKeys_AreDistinct_AndStartWithHistory()
    {
        var keys = Enum.GetValues<HistoryEntryKind>().Select(HistoryTimeline.TitleKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.StartsWith("History", key, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void EveryTitleAndSourceKey_HasAText(string culture)
    {
        var strings = new ResourceManager("NoMorePaper.Web.Client.Localization.Strings", typeof(Strings).Assembly);
        // German is the neutral resource (Strings.resx, no satellite), English has its own file.
        var info = culture == "de" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
        var keys = Enum.GetValues<HistoryEntryKind>().Select(HistoryTimeline.TitleKey)
            .Concat(["HistoryVia", "HistoryHint"])
            .Concat(_knownSources.Select(source => HistoryTimeline.SourceKey(source)!));

        // Without parents: GetString would fall back to German and hide a missing English text.
        var set = strings.GetResourceSet(info, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(set.GetString(key)), $"{culture}: {key}"));
    }
}
