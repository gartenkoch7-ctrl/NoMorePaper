using System.Text.Json;
using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Reads the model's answer to the mail check (<see cref="MailSchema"/>) and applies the checks after the
/// answer: a known sender is never spam, a spam mail has no note, a note whose dates are not in the mail
/// text, or a long number that is not, is dropped. Pure logic.
/// </summary>
public static class MailAnswer
{
    /// <param name="mailText">The text the model got, to check the note's dates against.</param>
    /// <param name="noteDropped">The note named a date or long number that is not in the text and was dropped.</param>
    /// <exception cref="InvalidDataException">The answer is no JSON object with the two verdicts.</exception>
    public static MailAssessment Parse(string content, bool knownSender, string mailText, out bool noteDropped)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(mailText);
        noteDropped = false;
        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("isSpam", out var spam) || spam.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !root.TryGetProperty("isDocument", out var document) || document.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("The answer lacks the verdicts.");
            }

            var isSpam = !knownSender && spam.GetBoolean();
            var note = root.TryGetProperty("note", out var value) && value.ValueKind == JsonValueKind.String ? Plain(value.GetString()) : null;
            if (isSpam || string.IsNullOrEmpty(note))
            {
                note = null;
            }
            else if (!MailNoteCheck.DatesAreInText(note, mailText) || !MailNoteCheck.NumbersAreInText(note, mailText))
            {
                noteDropped = true;
                note = null;
            }

            return new MailAssessment(isSpam, document.GetBoolean(), note);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The answer is no JSON.", exception);
        }
    }

    // The model likes Markdown emphasis (measured: "**17,39 EUR**"); a screen reader would read the stars.
    private static string? Plain(string? note) =>
        note?.Replace("**", string.Empty, StringComparison.Ordinal).Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal).Trim();
}
