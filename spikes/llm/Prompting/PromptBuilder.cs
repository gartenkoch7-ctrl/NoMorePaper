using System.Text;
using System.Text.Json;
using LlmSpike.Expected;
using LlmSpike.Text;

namespace LlmSpike.Prompting;

/// <summary>
/// Builds the German prompt (fourth version, see notes.md). Everything that stays the same between
/// documents (rules, lists) is the system text: Ollama reuses the evaluated prefix, which saves
/// about 20 seconds per document on four CPU cores.
/// </summary>
public static class PromptBuilder
{
    private const string Rules = """
        Du ordnest Dokumente eines privaten Haushalts ein. Du bekommst den Text eines Dokuments, wie ihn die Texterkennung (OCR) geliefert hat. Er kann Erkennungsfehler enthalten: fehlende Umlaute, verwechselte Zeichen (l/I/1, O/0, rn/m, B/ß), zerrissene Tabellen.

        Antworte mit genau einem JSON-Objekt mit diesen Feldern:
        - correspondent: Absender, also wer das Dokument ausgestellt hat (Firma, Behörde, Praxis, Verein oder Person). Nie der Empfänger. Ohne Rechtsform (GmbH, AG, e.V.), ohne Abteilung, ohne Adresse.
        - documentType: Dokumentart als ein Begriff. Sie beschreibt die Form des Dokuments (Rechnung, Vertrag, Bescheid, Schreiben), nicht sein Thema.
        - summary: 1 bis 2 deutsche Sätze: Worum geht es, was muss man wissen oder tun (Betrag, Frist).
        - title: kurzer deutscher Titel, höchstens 60 Zeichen, der das Dokument von ähnlichen unterscheidet (Gegenstand und Zeitraum oder Jahr). Ohne Adresse, ohne Namen des Empfängers.
        - documentDate: Tag, an dem das Dokument ausgestellt wurde, als JJJJ-MM-TT. Fast jedes Dokument hat ein solches Datum: Briefdatum neben dem Ort, Rechnungsdatum, „Auszug vom“, „Erstellt am“, Datum bei der Unterschrift; auch ausgeschrieben wie „22. Juli 2026“. Nicht Fälligkeit, Leistungszeitraum, Vertragsbeginn oder Geburtsdatum. null nur, wenn im ganzen Text kein Ausstellungsdatum mit Jahr steht.
        - amount: der wichtigste Geldbetrag als Zahl mit Punkt als Dezimaltrenner (1.234,56 EUR wird 1234.56). Das ist der Endbetrag, der zu zahlen ist oder erstattet wird (brutto, nach Abzug bereits geleisteter Zahlungen); bei Verträgen, Versicherungen und Beitragsänderungen der neue regelmäßige Gesamtbetrag (Gesamtmiete, Jahresbeitrag). Keine Zwischensummen, Nettobeträge, Steuern, Abschläge oder Einzelposten. null, wenn es keinen solchen Betrag gibt (zum Beispiel Arztbrief, Werbung, Kontoauszug, allgemeines Schreiben).
        - currency: Währung von amount als ISO-Code (EUR, USD, GBP, CHF). null, wenn amount null ist.
        - tags: 1 oder 2 deutsche Schlagwörter zum Thema des Dokuments. Nimm ein vorhandenes Schlagwort, wenn es das Thema wirklich trifft, sonst ein neues (ein Wort). Nicht die Dokumentart, nicht der Absender, kein Ort, kein Name.

        Der Haushalt hat bereits die folgenden Einträge. Steht der Absender oder die Dokumentart in der Liste, übernimm genau diese Schreibweise. Passt kein Eintrag, schlage einen neuen, kurzen Namen vor (zum Beispiel eine neue Dokumentart wie Angebot oder Quittung).
        """;

    private const string Closing =
        "Antworte auf Deutsch, auch wenn das Dokument in einer anderen Sprache geschrieben ist. " +
        "Erfinde nichts: Was nicht im Text steht, ist null.";

    public static string System(Household household, string? cacheBuster = null)
    {
        var builder = new StringBuilder();
        if (cacheBuster is not null)
        {
            // Measurement only: a changing first line defeats the prefix cache (cold request).
            builder.Append("Anfrage ").AppendLine(cacheBuster);
        }

        builder.AppendLine(Rules);
        builder.Append("Absender: ").AppendJoin("; ", household.Correspondents).AppendLine();
        builder.Append("Dokumentarten: ").AppendJoin("; ", household.DocumentTypes).AppendLine();
        builder.Append("Schlagwörter: ").AppendJoin("; ", household.Tags).AppendLine();
        builder.AppendLine();
        builder.Append(Closing);
        return builder.ToString();
    }

    public static string User(string documentText, IReadOnlyList<PriorDocument> examples)
    {
        var builder = new StringBuilder();
        if (examples.Count > 0)
        {
            builder.AppendLine("Früher bestätigte Dokumente, deren Absender im Text vorkommt (Vorbild für Dokumentart, Schlagwörter und Titel):");
            foreach (var example in examples)
            {
                builder.AppendLine(JsonSerializer.Serialize(example, ExampleJson));
            }

            builder.AppendLine();
        }

        builder.AppendLine("Dokumenttext:");
        builder.AppendLine("<<<");
        builder.AppendLine(documentText);
        builder.AppendLine(">>>");
        // After a long text the rules are far away; a small model follows them better with a reminder.
        builder.Append(
            "Gib jetzt das JSON aus. Übernimm für correspondent und documentType die Schreibweise eines vorhandenen Eintrags, wenn einer passt. " +
            "documentDate ist das Ausstellungsdatum, amount der Endbetrag.");
        return builder.ToString();
    }

    /// <summary>
    /// Picks examples the way the worker can: without knowing the answer, by looking for list names
    /// in the OCR text. A bank statement therefore matches several correspondents.
    /// </summary>
    public static IReadOnlyList<PriorDocument> FindExamples(Household household, string documentText, int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        var haystack = " " + NameNormalizer.Normalize(documentText) + " ";
        return household.PriorDocuments
            .Select(prior => (prior, Position: haystack.IndexOf(" " + NameNormalizer.Normalize(prior.Correspondent) + " ", StringComparison.Ordinal)))
            .Where(match => match.Position >= 0)
            // The sender is named in the letterhead, so the earliest hit is the best guess.
            .OrderBy(match => match.Position)
            .Take(limit)
            .Select(match => match.prior)
            .ToList();
    }

    private static readonly JsonSerializerOptions ExampleJson = new(SpikeJson.Options) { WriteIndented = false };
}
