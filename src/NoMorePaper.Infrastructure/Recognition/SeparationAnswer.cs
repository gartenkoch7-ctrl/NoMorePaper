using System.Text.Json;
using NoMorePaper.Application.Separation;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Reads the model's answer to the stack separation (<see cref="SeparationSchema"/>) and keeps what is
/// plausible: boundaries within the pages asked about, after page 1, each once; one sender per page at most.
/// Pure logic.
/// </summary>
public static class SeparationAnswer
{
    /// <summary>Longest sender kept; the model names a sender, not a letterhead.</summary>
    public const int MaxSenderLength = 120;

    /// <param name="pageCount">How many pages the model was asked about.</param>
    /// <param name="dropped">How many boundaries were outside the pages or repeated.</param>
    /// <exception cref="InvalidDataException">The answer is no JSON object with both arrays.</exception>
    public static SeparationAssessment Parse(string content, int pageCount, out int dropped)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("boundaries", out var boundaries) || boundaries.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("senders", out var senders) || senders.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The answer lacks the boundaries or the senders.");
            }

            var found = new SortedSet<int>();
            dropped = 0;
            foreach (var value in boundaries.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var page) || page < 2 || page > pageCount || !found.Add(page))
                {
                    dropped++;
                }
            }

            var names = senders.EnumerateArray()
                .Take(pageCount)
                .Select(value => value.ValueKind == JsonValueKind.String ? Short(value.GetString()) : string.Empty)
                .ToList();
            return new SeparationAssessment([.. found], names);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The answer is no JSON.", exception);
        }
    }

    private static string Short(string? sender)
    {
        var trimmed = sender?.Trim() ?? string.Empty;
        return trimmed.Length > MaxSenderLength ? trimmed[..MaxSenderLength] : trimmed;
    }
}
