using System.Text;
using NoMorePaper.Application.Documents;
using Npgsql;
using NpgsqlTypes;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// The SQL shared by the document list and the search: the label columns (correspondent, type, date,
/// amount, recognition) with their joins, and the filters. Conditions are fixed text over
/// <c>documents d</c>; every value is a typed parameter, never part of the text.
/// </summary>
internal static class DocumentFilterSql
{
    /// <summary>Columns read by <see cref="LabelRow"/>; needs <see cref="LabelJoins"/>.</summary>
    public const string LabelColumns = """
        c.id AS "CorrespondentId", c.name AS "CorrespondentName",
        t.id AS "DocumentTypeId", t.name AS "DocumentTypeName",
        d.document_date AS "DocumentDate", d.amount AS "Amount", d.currency AS "Currency", d.recognition AS "Recognition"
        """;

    /// <summary>
    /// The entries of the document. A document the viewer may see makes its entries visible to the viewer
    /// (visibility rule of the lists), so no further filter is needed here.
    /// </summary>
    public const string LabelJoins =
        "LEFT JOIN correspondents c ON c.id = d.correspondent_id LEFT JOIN document_types t ON t.id = d.document_type_id";

    /// <summary>The conditions of <paramref name="filter"/>, each starting with <c>AND</c>; empty without filters.</summary>
    /// <param name="parameters">Receives one parameter per value, named <c>f_…</c>.</param>
    public static string Conditions(DocumentFilter filter, ICollection<NpgsqlParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(parameters);
        var sql = new StringBuilder();

        void Add(string condition, string name, object value, NpgsqlDbType type)
        {
            sql.Append(" AND ").Append(condition);
            parameters.Add(new NpgsqlParameter(name, type) { Value = value });
        }

        if (filter.CorrespondentId is { } correspondent)
        {
            Add("d.correspondent_id = @f_correspondent", "f_correspondent", correspondent, NpgsqlDbType.Uuid);
        }

        if (filter.DocumentTypeId is { } type)
        {
            Add("d.document_type_id = @f_type", "f_type", type, NpgsqlDbType.Uuid);
        }

        if (filter.From is { } from)
        {
            Add("d.document_date >= @f_from", "f_from", from, NpgsqlDbType.Date);
        }

        if (filter.To is { } to)
        {
            Add("d.document_date <= @f_to", "f_to", to, NpgsqlDbType.Date);
        }

        if (filter.AmountFrom is { } amountFrom)
        {
            Add("d.amount >= @f_amount_from", "f_amount_from", amountFrom, NpgsqlDbType.Numeric);
        }

        if (filter.AmountTo is { } amountTo)
        {
            Add("d.amount <= @f_amount_to", "f_amount_to", amountTo, NpgsqlDbType.Numeric);
        }

        if (filter.IsPrivate is { } isPrivate)
        {
            Add("d.is_private = @f_private", "f_private", isPrivate, NpgsqlDbType.Boolean);
        }

        var tags = filter.DistinctTagIds;
        if (tags.Count > 0)
        {
            // (document_id, tag_id) is the key of document_tags, so the count is of distinct tags.
            var condition = filter.AllTags
                ? "(SELECT count(*) FROM document_tags dt WHERE dt.document_id = d.id AND dt.tag_id = ANY(@f_tags)) = cardinality(@f_tags)"
                : "EXISTS (SELECT 1 FROM document_tags dt WHERE dt.document_id = d.id AND dt.tag_id = ANY(@f_tags))";
            Add(condition, "f_tags", tags.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        }

        return sql.ToString();
    }
}
