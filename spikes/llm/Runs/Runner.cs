using System.Globalization;
using System.Text.Json;
using LlmSpike.Ollama;
using LlmSpike.Prompting;
using LlmSpike.Expected;
using LlmSpike.Text;

namespace LlmSpike.Runs;

/// <summary>Sends every selected sample to one model and records the raw answers.</summary>
public sealed class Runner(OllamaClient ollama, SpikeOptions options, TextWriter log)
{
    public async Task<RunFile> RunAsync(SampleSet samples, CancellationToken cancellationToken)
    {
        var version = await ollama.VersionAsync(cancellationToken).ConfigureAwait(false);
        var started = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var schema = RecognitionSchema.Create();
        var records = new List<RunRecord>();

        foreach (var document in samples.Documents.Where(d => options.Only.Count == 0 || options.Only.Contains(d.Id[..2])))
        {
            var full = TextTruncator.Compact(SampleSet.ReadText(options.SamplesDirectory, document));
            var sent = TextTruncator.HeadAndTail(full, options.HeadChars, options.TailChars);
            var examples = PromptBuilder.FindExamples(samples.Household, sent, options.Examples);
            var system = PromptBuilder.System(samples.Household, options.ColdPrefix ? document.Id : null);
            var user = PromptBuilder.User(sent, examples);

            RunRecord record;
            try
            {
                // DeepClone: a JsonNode can only have one parent, and each request owns its body.
                var result = await ollama
                    .ChatAsync(options.Model, system, user, schema.DeepClone().AsObject(), options.ContextTokens, options.Threads, cancellationToken)
                    .ConfigureAwait(false);
                record = new RunRecord(
                    document.Id, full.Length, sent.Length, examples.Count, result.WallSeconds, result.LoadSeconds,
                    result.PromptSeconds, result.PromptTokens, result.EvalSeconds, result.EvalTokens,
                    result.DoneReason, result.Content, null);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            {
                record = new RunRecord(document.Id, full.Length, sent.Length, examples.Count, 0, 0, 0, 0, 0, 0, null, string.Empty, exception.Message);
            }

            records.Add(record);
            await log.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"{document.Id,-28} {record.SentChars,6} chars  {record.PromptTokens,5} + {record.EvalTokens,3} tok  {record.WarmSeconds,6:F1} s  {record.Error ?? record.DoneReason}")).ConfigureAwait(false);
        }

        var loaded = await ollama.LoadedBytesAsync(options.Model, cancellationToken).ConfigureAwait(false);
        var header = new RunHeader(
            options.Label, options.Model, version, options.ContextTokens, options.Threads,
            options.HeadChars, options.TailChars, options.Examples, options.ColdPrefix, started, loaded);
        return new RunFile(header, records);
    }
}
