using System.Text;
using LlmSpike;
using LlmSpike.Ollama;
using LlmSpike.Reporting;
using LlmSpike.Runs;
using LlmSpike.Expected;

Console.OutputEncoding = Encoding.UTF8;
var options = SpikeOptions.Parse(args);
var samples = SampleSet.Load(options.SamplesDirectory);

switch (options.Command)
{
    case "run" when options.Model.Length > 0:
    {
        using var ollama = new OllamaClient(options.Endpoint);
        var run = await new Runner(ollama, options, Console.Out).RunAsync(samples, CancellationToken.None);
        run.Save(options.RunsDirectory);
        return 0;
    }

    case "report":
    {
        var notesPath = Path.Combine(options.SpikeDirectory, "notes.md");
        var notes = File.Exists(notesPath) ? File.ReadAllText(notesPath) : null;
        var report = new ReportWriter(samples).Write(RunFile.LoadAll(options.RunsDirectory), notes);
        File.WriteAllText(Path.Combine(options.SpikeDirectory, "results.md"), report);
        Console.WriteLine(report);
        return 0;
    }

    case "show":
    {
        var printer = new AnswerPrinter(samples, Console.Out);
        foreach (var run in RunFile.LoadAll(options.RunsDirectory).Where(run => run.Header.Label == options.Label))
        {
            printer.Print(run);
        }

        return 0;
    }

    default:
        Console.WriteLine("""
            Usage:
              run --model <name> [--label <run>] [--num-ctx 4096] [--threads 4] [--head 4500 --tail 1500]
                  [--examples 0] [--cold-prefix] [--only 01,03] [--url http://127.0.0.1:11435/]
              report
              show --label <run>
            --head 0 sends the full text. See README.md.
            """);
        return 1;
}
