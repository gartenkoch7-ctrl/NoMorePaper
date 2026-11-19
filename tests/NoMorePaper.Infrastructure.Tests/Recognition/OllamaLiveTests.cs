using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

/// <summary>
/// Against a real Ollama with the model already loaded, only when <c>NOMOREPAPER_TEST_OLLAMA</c> names its
/// address (e.g. <c>http://127.0.0.1:11500</c>); skipped otherwise, also in CI. Never downloads the model.
/// </summary>
public sealed class OllamaLiveTests
{
    private const string EndpointVariable = "NOMOREPAPER_TEST_OLLAMA";

    private const string Letter = """
        Stadtwerke München GmbH · Emmy-Noether-Straße 2 · 80992 München
        Herrn Max Mustermann, Lindenstraße 5, 80331 München
        München, 01.11.2026
        Abschlagsrechnung Strom November 2026
        Kundennummer 4711-0815
        Ihr monatlicher Abschlag für Strom beträgt 84,20 EUR (brutto).
        Der Betrag wird am 15.11.2026 von Ihrem Konto abgebucht.
        Mit freundlichen Grüßen, Ihre Stadtwerke München
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recognize_ReadsALetter_WithTheConfiguredModel()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(endpoint), $"Set {EndpointVariable} to run against a real Ollama.");
        var settings = new OllamaSettings(new Uri(endpoint!.TrimEnd('/') + "/"), OllamaSettings.DefaultModel, OllamaSettings.DefaultTimeout, 4);
        using var recognizer = new OllamaRecognizer(
            new HttpClient { BaseAddress = settings.Endpoint, Timeout = Timeout.InfiniteTimeSpan },
            settings,
            TimeProvider.System,
            NullLogger<OllamaRecognizer>.Instance);
        var request = new RecognitionRequest(
            RecognitionText.Shorten(Letter), ["Stadtwerke München", "AOK Bayern"], ["Rechnung", "Bescheid"], ["Strom"], []);

        var result = await recognizer.RecognizeAsync(request, Ct);

        Assert.Equal(("Stadtwerke München", "Rechnung"), (result.Correspondent, result.DocumentType));
        Assert.Equal((new DateOnly(2026, 11, 1), 84.2m, "EUR"), (result.DocumentDate, result.Amount?.Value, result.Amount?.Currency));
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
    }
}
