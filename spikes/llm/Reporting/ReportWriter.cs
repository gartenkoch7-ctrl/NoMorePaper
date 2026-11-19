using System.Globalization;
using System.Text;
using LlmSpike.Runs;
using LlmSpike.Expected;
using LlmSpike.Scoring;

namespace LlmSpike.Reporting;

/// <summary>Scores all stored runs and writes <c>results.md</c> (German, like the rest of the docs).</summary>
public sealed class ReportWriter(SampleSet samples)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public string Write(IReadOnlyList<RunFile> runs, string? notes)
    {
        var scored = runs.Select(run => (Run: run, Scores: this.Score(run))).ToList();
        var text = new StringBuilder();
        text.AppendLine("# LLM-Spike: Messergebnisse");
        text.AppendLine();
        text.AppendLine("Erzeugt mit `dotnet run --project spikes/llm -- report` aus den Rohantworten in `measurements/*.json`.");
        text.AppendLine("Nicht von Hand ändern; Notizen stehen in `notes.md` und werden unten angehängt.");
        text.AppendLine();

        text.AppendLine("## Läufe");
        text.AppendLine();
        text.AppendLine("| Lauf | Modell | Kontext | Threads | Kürzung Kopf+Fuß | Beispiele | ohne Präfix-Cache | Speicher laut Ollama |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var (run, _) in scored)
        {
            var h = run.Header;
            var cut = h.HeadChars <= 0 ? "keine" : $"{h.HeadChars}+{h.TailChars}";
            text.AppendLine(German, $"| {h.Label} | `{h.Model}` | {h.ContextTokens} | {h.Threads} | {cut} | {h.Examples} | {(h.ColdPrefix ? "ja" : "nein")} | {h.LoadedBytes / 1e9:F1} GB |");
        }

        text.AppendLine();
        text.AppendLine("## Trefferquote je Feld");
        text.AppendLine();
        text.AppendLine("„Absender“ und „Art“: normalisierter Vergleich mit Sollwert oder zugelassenem Alias. „Betrag“: Zahl und");
        text.AppendLine("Währung. „Titel“ und „Zusammenfassung“: einfache Regeln (Länge, Stichwort, deutsch), siehe `Scoring/`.");
        text.AppendLine();
        text.AppendLine("| Lauf | n | Absender | Art | Datum | Betrag | Tags ≥ 1 | Titel | Zusammenfassung |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (run, scores) in scored)
        {
            string Rate(Func<DocumentScore, bool> field) => Share(scores.Count(field), scores.Count);
            text.AppendLine(German, $"| {run.Header.Label} | {scores.Count} | {Rate(s => s.CorrespondentMatch)} | {Rate(s => s.TypeMatch)} | {Rate(s => s.Date)} | {Rate(s => s.AmountAndCurrency)} | {Rate(s => s.TagHit)} | {Rate(s => s.Title)} | {Rate(s => s.Summary)} |");
        }

        text.AppendLine();
        text.AppendLine("## Listen und Schlagwörter");
        text.AppendLine();
        text.AppendLine("„aus Liste“: Der Sollwert steht in der Liste des Haushalts und das Modell liefert genau diese Schreibweise.");
        text.AppendLine("„Tag-Abdeckung“: Anteil der erwarteten Schlagwörter, die geliefert wurden. „Tag-Genauigkeit“: Anteil der");
        text.AppendLine("gelieferten Schlagwörter, die erwartet oder zugelassen sind. „vorhandene Tags“: Anteil aus der Liste.");
        text.AppendLine();
        text.AppendLine("| Lauf | Absender aus Liste | Art aus Liste | Betrag (nur Zahl) | Tag-Abdeckung | Tag-Genauigkeit | vorhandene Tags | gültiges JSON |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var (run, scores) in scored)
        {
            string Listed(Func<DocumentScore, bool?> field) => Share(scores.Count(s => field(s) == true), scores.Count(s => field(s) is not null));
            string Mean(Func<DocumentScore, double> field) => scores.Count == 0 ? "–" : scores.Average(field).ToString("P0", German);
            text.AppendLine(German, $"| {run.Header.Label} | {Listed(s => s.CorrespondentFromList)} | {Listed(s => s.TypeFromList)} | {Share(scores.Count(s => s.Amount), scores.Count)} | {Mean(s => s.TagRecall)} | {Mean(s => s.TagPrecision)} | {Mean(s => s.TagsFromList)} | {Share(scores.Count(s => s.ValidJson), scores.Count)} |");
        }

        text.AppendLine();
        text.AppendLine("## Dauer je Dokument");
        text.AppendLine();
        text.AppendLine("Sekunden je Anfrage ohne das Laden des Modells. „Laden“ ist die Ladezeit vor dem ersten Dokument.");
        text.AppendLine();
        text.AppendLine("| Lauf | Median | Mittel | Max | ≤ 60 s | Prompt-Tokens (Mittel/Max) | Antwort-Tokens | Prompt tok/s | Antwort tok/s | Laden |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var (run, _) in scored)
        {
            var ok = run.Records.Where(r => r.Error is null).ToList();
            if (ok.Count == 0)
            {
                continue;
            }

            var seconds = ok.Select(r => r.WarmSeconds).Order().ToList();
            var median = seconds[seconds.Count / 2];
            // With the prefix cache Ollama still reports the full prompt length, so the rate is only real without it.
            var promptRate = run.Header.ColdPrefix
                ? (ok.Sum(r => r.PromptTokens) / Math.Max(0.001, ok.Sum(r => r.PromptSeconds))).ToString("F1", German)
                : "–";
            var evalRate = ok.Sum(r => r.EvalTokens) / Math.Max(0.001, ok.Sum(r => r.EvalSeconds));
            text.AppendLine(German, $"| {run.Header.Label} | {median:F1} s | {seconds.Average():F1} s | {seconds[^1]:F1} s | {Share(seconds.Count(s => s <= 60), seconds.Count)} | {ok.Average(r => r.PromptTokens):F0} / {ok.Max(r => r.PromptTokens)} | {ok.Average(r => r.EvalTokens):F0} | {promptRate} | {evalRate:F1} | {ok.Max(r => r.LoadSeconds):F1} s |");
        }

        text.AppendLine();
        text.AppendLine("## Abweichungen je Dokument");
        text.AppendLine();
        foreach (var (run, scores) in scored.Where(entry => entry.Run.Header.Label.StartsWith("main", StringComparison.Ordinal)))
        {
            text.AppendLine(German, $"### {run.Header.Label}");
            text.AppendLine();
            foreach (var score in scores.Where(s => s.Misses.Count > 0))
            {
                var seconds = run.Records.First(r => r.Id == score.Id).WarmSeconds;
                text.AppendLine(German, $"- {score.Id} ({seconds:F0} s): {string.Join("; ", score.Misses)}");
            }

            text.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            text.AppendLine(notes.Trim());
        }

        return text.ToString();
    }

    private List<DocumentScore> Score(RunFile run) =>
        run.Records
            .Where(record => record.Error is null)
            .Select(record => FieldScorer.Score(samples.Documents.First(d => d.Id == record.Id), record.Content, samples.Household))
            .ToList();

    private static string Share(int hits, int total) =>
        total == 0 ? "–" : string.Create(German, $"{hits}/{total} ({(double)hits / total:P0})");
}
