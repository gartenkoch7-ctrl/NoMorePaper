using System.Globalization;
using LlmSpike.Runs;
using LlmSpike.Expected;
using LlmSpike.Scoring;

namespace LlmSpike.Reporting;

/// <summary>Prints every answer of one run next to the rule verdicts, for the manual spot check.</summary>
public sealed class AnswerPrinter(SampleSet samples, TextWriter output)
{
    public void Print(RunFile run)
    {
        foreach (var record in run.Records)
        {
            var expected = samples.Documents.First(d => d.Id == record.Id);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"## {record.Id}  {record.WarmSeconds:F0} s  {record.Error}"));
            var answer = Recognition.Parse(record.Content);
            if (answer is null)
            {
                output.WriteLine(record.Content);
                continue;
            }

            output.WriteLine($"   {answer.Correspondent} | {answer.DocumentType} | {answer.DocumentDate} | {answer.Amount?.ToString(CultureInfo.InvariantCulture)} {answer.Currency} | {string.Join(", ", answer.Tags)}");
            output.WriteLine($"   T: {answer.Title}");
            output.WriteLine($"   Z: {answer.Summary}");
            foreach (var miss in FieldScorer.Score(expected, record.Content, samples.Household).Misses)
            {
                output.WriteLine($"   ! {miss}");
            }
        }
    }
}
