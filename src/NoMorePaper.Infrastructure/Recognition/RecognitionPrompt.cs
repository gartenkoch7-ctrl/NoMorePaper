using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// The German prompt from the spike (<c>spikes/llm/Prompting/PromptBuilder.cs</c>, fourth version). What
/// stays the same between documents of one owner (rules, lists) is the system text: Ollama reuses the
/// evaluated prefix, which saves about 20 seconds per document on four CPU cores. Line ends are always
/// <c>\n</c>, whatever the platform, so the prefix stays the same.
/// </summary>
public static class RecognitionPrompt
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

    private const string ExamplesIntro =
        "Früher bestätigte Dokumente, deren Absender im Text vorkommt (Vorbild für Dokumentart, Schlagwörter und Titel):";

    // After a long text the rules are far away; a small model follows them better with a reminder.
    private const string Reminder =
        "Gib jetzt das JSON aus. Übernimm für correspondent und documentType die Schreibweise eines vorhandenen Eintrags, wenn einer passt. " +
        "documentDate ist das Ausstellungsdatum, amount der Endbetrag.";

    private static readonly JsonSerializerOptions s_exampleJson = new(JsonSerializerDefaults.Web)
    {
        // Umlauts as they are: fewer tokens, and the model reads them as words.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Rules and the owner's lists.</summary>
    public static string System(RecognitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        builder.Append(Rules.ReplaceLineEndings("\n")).Append('\n');
        builder.Append("Absender: ").AppendJoin("; ", request.Correspondents).Append('\n');
        builder.Append("Dokumentarten: ").AppendJoin("; ", request.DocumentTypes).Append('\n');
        builder.Append("Schlagwörter: ").AppendJoin("; ", request.Tags).Append('\n');
        builder.Append('\n');
        builder.Append(Closing);
        return builder.ToString();
    }

    /// <summary>Examples (one JSON line each, without their text) and the shortened document text.</summary>
    public static string User(RecognitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        if (request.Examples.Count > 0)
        {
            builder.Append(ExamplesIntro).Append('\n');
            foreach (var example in request.Examples)
            {
                builder.Append(JsonSerializer.Serialize(example, s_exampleJson)).Append('\n');
            }

            builder.Append('\n');
        }

        builder.Append("Dokumenttext:\n<<<\n");
        builder.Append(request.Text.ReplaceLineEndings("\n")).Append('\n');
        builder.Append(">>>\n");
        builder.Append(Reminder);
        return builder.ToString();
    }
}
