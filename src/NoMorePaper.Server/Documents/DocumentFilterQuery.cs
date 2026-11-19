using System.Globalization;
using NoMorePaper.Application.Documents;

namespace NoMorePaper.Server.Documents;

/// <summary>
/// The filter query parameters of <c>GET /api/documents</c> and <c>GET /api/search</c>:
/// <c>correspondentId</c>, <c>documentTypeId</c>, <c>from</c>, <c>to</c> (<c>yyyy-MM-dd</c>),
/// <c>tagIds</c> (comma-separated), <c>allTags</c>, <c>amountFrom</c>, <c>amountTo</c> (invariant
/// decimal, e.g. <c>84.20</c>), <c>isPrivate</c>. An empty value is no filter; an unreadable or repeated
/// one is an error (400), never silently ignored.
/// </summary>
internal sealed class DocumentFilterQuery
{
    private DocumentFilterQuery(DocumentFilter? filter, string? error)
    {
        Filter = filter;
        Error = error;
    }

    /// <summary>The filter; <c>null</c> when <see cref="Error"/> is set.</summary>
    public DocumentFilter? Filter { get; }

    /// <summary>What could not be read, for the 400 answer.</summary>
    public string? Error { get; }

    /// <summary>Minimal API binding: the endpoint answers 400 itself when <see cref="Error"/> is set.</summary>
    public static ValueTask<DocumentFilterQuery?> BindAsync(HttpContext context) =>
        ValueTask.FromResult<DocumentFilterQuery?>(Parse(context.Request.Query));

    public static DocumentFilterQuery Parse(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var errors = new List<string>();
        T? Read<T>(string key, TryParser<T> parse)
            where T : struct
        {
            var values = query[key];
            if (values.Count == 0 || (values.Count == 1 && string.IsNullOrWhiteSpace(values[0])))
            {
                return null;
            }

            if (values.Count == 1 && parse(values[0]!.Trim(), out var value))
            {
                return value;
            }

            errors.Add(key);
            return null;
        }

        var filter = new DocumentFilter(
            Read<Guid>("correspondentId", Guid.TryParse),
            Read<Guid>("documentTypeId", Guid.TryParse),
            Read<DateOnly>("from", TryParseDate),
            Read<DateOnly>("to", TryParseDate),
            ReadTags(query["tagIds"], errors),
            Read<bool>("allTags", bool.TryParse) ?? false,
            Read<decimal>("amountFrom", TryParseAmount),
            Read<decimal>("amountTo", TryParseAmount),
            Read<bool>("isPrivate", bool.TryParse));

        return errors.Count == 0
            ? new DocumentFilterQuery(filter, null)
            : new DocumentFilterQuery(null, "Unreadable filter: " + string.Join(", ", errors) + ".");
    }

    private delegate bool TryParser<T>(string text, out T value);

    private static List<Guid>? ReadTags(Microsoft.Extensions.Primitives.StringValues values, List<string> errors)
    {
        if (values.Count == 0 || (values.Count == 1 && string.IsNullOrWhiteSpace(values[0])))
        {
            return null;
        }

        var parts = values.Count == 1
            ? values[0]!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        if (parts.Length == 0 || !parts.All(part => Guid.TryParse(part, out _)))
        {
            errors.Add("tagIds");
            return null;
        }

        return [.. parts.Select(Guid.Parse)];
    }

    private static bool TryParseDate(string text, out DateOnly value) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static bool TryParseAmount(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
}
