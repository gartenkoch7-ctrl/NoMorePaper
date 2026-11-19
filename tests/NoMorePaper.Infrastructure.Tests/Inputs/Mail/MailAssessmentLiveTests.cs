using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Infrastructure.Inputs.Mail;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>
/// Measures the mail check on the 24 synthetic sample mails against a real Ollama with the model already
/// loaded, only when <c>NOMOREPAPER_TEST_OLLAMA</c> names its address; skipped otherwise, also in CI. Writes
/// the hit rates to the test output and to <c>mail-assessment.txt</c> next to the tests.
/// </summary>
public sealed class MailAssessmentLiveTests
{
    private const string EndpointVariable = "NOMOREPAPER_TEST_OLLAMA";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSampleMails_NoImportantDocumentIsSpam()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(endpoint), $"Set {EndpointVariable} to run against a real Ollama.");
        var settings = new OllamaSettings(new Uri(endpoint!.TrimEnd('/') + "/"), OllamaSettings.DefaultModel, OllamaSettings.DefaultTimeout, 4);
        using var assessor = new OllamaMailAssessor(
            new HttpClient { BaseAddress = settings.Endpoint, Timeout = Timeout.InfiniteTimeSpan },
            settings,
            new LlmGate(TimeProvider.System, NullLogger<LlmGate>.Instance),
            TimeProvider.System,
            NullLogger<OllamaMailAssessor>.Instance);

        var report = new StringBuilder();
        var results = new List<(Sample Sample, MailAssessment Answer, long Ms)>();
        foreach (var sample in Samples())
        {
            var content = MailMessageReader.Read(TestMessages.Sample(sample.File), null, DateTimeOffset.UtcNow);
            var types = content.Attachments.Select(MailIntake.DocumentType).OfType<string>().ToList();
            var watch = Stopwatch.StartNew();
            var answer = await assessor.AssessAsync(MailText.From(content, types), knownSender: false, Ct);
            results.Add((sample, answer, watch.ElapsedMilliseconds));
            report.AppendLine(CultureInfo.InvariantCulture, $"{sample.File}: spam={answer.IsSpam} document={answer.IsDocument} {watch.ElapsedMilliseconds} ms note={answer.Note}");
        }

        var spam = results.Where(result => result.Sample.Spam == true).ToList();
        var important = results.Where(result => result.Sample.Spam == false).ToList();
        var documents = results.Where(result => result.Sample.Document is not null).ToList();
        var newsletters = results.Where(result => result.Sample.Category == "newsletter").ToList();
        report.AppendLine(CultureInfo.InvariantCulture, $"spam recognized: {spam.Count(r => r.Answer.IsSpam)}/{spam.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"important taken as spam: {important.Count(r => r.Answer.IsSpam)}/{important.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"document question right: {documents.Count(r => r.Answer.IsDocument == r.Sample.Document)}/{documents.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"newsletters as spam: {newsletters.Count(r => r.Answer.IsSpam)}/{newsletters.Count}");
        var times = results.Select(result => result.Ms).Order().ToList();
        report.AppendLine(CultureInfo.InvariantCulture, $"time per mail: median {times[times.Count / 2]} ms, max {times[^1]} ms");
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "mail-assessment.txt"), report.ToString(), Ct);

        Assert.DoesNotContain(important, result => result.Answer.IsSpam);
    }

    private static List<Sample> Samples()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Inputs", "Mail", "Samples", "expected.json"));
        return JsonSerializer.Deserialize<List<Sample>>(json, s_json)!;
    }

    private sealed record Sample(string File, string Category, bool? Spam, bool? Document);
}
