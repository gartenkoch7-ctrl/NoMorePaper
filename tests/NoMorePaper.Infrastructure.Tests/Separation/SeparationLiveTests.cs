using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Separation;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>
/// The measurement of the stack separation (goal: at least 80 % of the boundaries of the 12 sample stacks)
/// against a real Ollama with the model already loaded, only when <c>NOMOREPAPER_TEST_OLLAMA</c> names its
/// address; skipped otherwise, also in CI. Never downloads the model.
/// </summary>
public sealed class SeparationLiveTests
{
    private const string EndpointVariable = "NOMOREPAPER_TEST_OLLAMA";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSamples_AreSeparated_AtLeastEightyPercentRight()
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

        var share = (double)right / expected;
        TestContext.Current.SendDiagnosticMessage(
            string.Create(CultureInfo.InvariantCulture, $"{right} of {expected} boundaries right ({share:P0}), {wrong} wrong"));
        Assert.True(share >= 0.8, string.Create(CultureInfo.InvariantCulture, $"Only {share:P0} of the boundaries were found ({wrong} wrong)."));
    }
}
