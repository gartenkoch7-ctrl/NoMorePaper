using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Separation;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>
/// The measurement of the stack separation (goal: at least 80 % of the boundaries of the 13 sample stacks,
/// and wrong boundaries at most a fifth of the expected ones)
/// against a real Ollama with the model already loaded, only when <c>NOMOREPAPER_TEST_OLLAMA</c> names its
/// address; skipped otherwise, also in CI. Never downloads the model.
/// </summary>
public sealed class SeparationLiveTests
{
    private const string EndpointVariable = "NOMOREPAPER_TEST_OLLAMA";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSamples_AreSeparated_EightyPercentFound_AndFewWrong()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(endpoint), $"Set {EndpointVariable} to run against a real Ollama.");
        var settings = new OllamaSettings(new Uri(endpoint!.TrimEnd('/') + "/"), OllamaSettings.DefaultModel, OllamaSettings.DefaultTimeout, 4);
        using var assessor = new OllamaSeparationAssessor(
            new HttpClient { BaseAddress = settings.Endpoint, Timeout = Timeout.InfiniteTimeSpan },
            settings,
            new LlmGate(TimeProvider.System, NullLogger<LlmGate>.Instance),
            TimeProvider.System,
            NullLogger<OllamaSeparationAssessor>.Instance);

        int expected = 0, right = 0, wrong = 0;
        foreach (var sample in SeparationSamples.Load())
        {
            var assessment = await assessor.AssessAsync(sample.Pages, Ct);
            var found = SeparationSamples.Boundaries(SeparationHeuristics.Find(sample.Pages).ToSuggestion(assessment.Boundaries));
            expected += sample.Boundaries.Count;
            right += found.Intersect(sample.Boundaries).Count();
            wrong += found.Except(sample.Boundaries).Count();
            TestContext.Current.SendDiagnosticMessage(
                $"{sample.Name}: expected {string.Join(",", sample.Boundaries)}, found {string.Join(",", found)}, model {string.Join(",", assessment.Boundaries)}");
        }

        // A wrong boundary costs the user as much as a missed one: both need a correction in "to review".
        var recall = (double)right / expected;
        var precision = right + wrong == 0 ? 1 : (double)right / (right + wrong);
        var f1 = recall + precision == 0 ? 0 : 2 * recall * precision / (recall + precision);
        var summary = string.Create(
            CultureInfo.InvariantCulture, $"{right} of {expected} boundaries right ({recall:P0}), {wrong} wrong (precision {precision:P0}, F1 {f1:F2})");
        TestContext.Current.SendDiagnosticMessage(summary);
        Assert.True(recall >= 0.8, "Too few boundaries found: " + summary);
        Assert.True(wrong <= 0.2 * expected, "Too many wrong boundaries: " + summary);
    }
}
