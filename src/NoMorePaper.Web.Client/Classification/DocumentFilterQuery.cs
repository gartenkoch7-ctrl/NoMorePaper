using System.Globalization;
using System.Text;
using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>Turns a filter into the query string of the document list (names as in the API contract).</summary>
public static class DocumentFilterQuery
{
    /// <summary>The filter as <c>&amp;name=value</c> pairs (empty for no filter); values use the invariant culture.</summary>
    public static string Build(DocumentFilterDto? filter)
    {
        if (filter is null)
        {
            return string.Empty;
        }

        var query = new StringBuilder();
        Add(query, "correspondentId", filter.CorrespondentId?.ToString());
        Add(query, "documentTypeId", filter.DocumentTypeId?.ToString());
        Add(query, "from", filter.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(query, "to", filter.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (filter.TagIds is { Count: > 0 } tags)
        {
            query.Append("&tagIds=").Append(string.Join(',', tags));
            if (filter.AllTags)
            {
                query.Append("&allTags=true");
            }
        }

        Add(query, "amountFrom", filter.AmountFrom?.ToString(CultureInfo.InvariantCulture));
        Add(query, "amountTo", filter.AmountTo?.ToString(CultureInfo.InvariantCulture));
        Add(query, "isPrivate", filter.IsPrivate is { } isPrivate ? (isPrivate ? "true" : "false") : null);
        return query.ToString();
    }

    /// <summary>How many filters are switched on (the "all tags" option belongs to the tags and counts with them).</summary>
    public static int ActiveCount(DocumentFilterDto? filter) => filter is null
        ? 0
        : new object?[]
        {
            filter.CorrespondentId, filter.DocumentTypeId, filter.From, filter.To,
            filter.TagIds is { Count: > 0 } ? filter.TagIds : null,
            filter.AmountFrom, filter.AmountTo, filter.IsPrivate,
        }.Count(value => value is not null);

    /// <summary>The filter of the bar together with the document type of the chips; <c>null</c> when both are off.</summary>
    public static DocumentFilterDto? Combine(DocumentFilterDto? bar, Guid? typeId) =>
        ActiveCount(bar) == 0 && typeId is null ? null : (bar ?? new DocumentFilterDto()) with { DocumentTypeId = typeId };

    private static void Add(StringBuilder query, string name, string? value)
    {
        if (value is not null)
        {
            query.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }
    }
}
