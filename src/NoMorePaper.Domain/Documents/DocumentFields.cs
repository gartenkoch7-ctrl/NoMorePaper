namespace NoMorePaper.Domain.Documents;

/// <summary>
/// The fields recognition proposes and editors change, as one valid set: title, correspondent,
/// document type, date, amount, summary and tags. A <c>null</c> field is empty, except
/// <see cref="Title"/>, where <c>null</c> keeps the current title (a document always has one).
/// </summary>
public sealed class DocumentFields
{
    /// <summary>Longest summary kept; the summary is meant to be one or two sentences.</summary>
    public const int MaxSummaryLength = 1000;

    /// <param name="title">New title, or <c>null</c> to keep the current one.</param>
    /// <param name="correspondentId">Id of a <see cref="Classification.Correspondent"/>.</param>
    /// <param name="documentTypeId">Id of a <see cref="Classification.DocumentType"/>.</param>
    /// <param name="documentDate">Date printed on the document (not when it was received).</param>
    /// <param name="amount">Amount to pay or paid.</param>
    /// <param name="summary">One or two sentences about the content.</param>
    /// <param name="tagIds">Ids of <see cref="Classification.Tag"/>s; duplicates are dropped.</param>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public DocumentFields(
        string? title,
        Guid? correspondentId,
        Guid? documentTypeId,
        DateOnly? documentDate,
        Money? amount,
        string? summary,
        IEnumerable<Guid> tagIds)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        if (title is not null)
        {
            Document.RequireTitle(title);
        }

        if (summary is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(summary);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(summary.Length, MaxSummaryLength, nameof(summary));
        }

        RequireId(correspondentId, nameof(correspondentId));
        RequireId(documentTypeId, nameof(documentTypeId));
        var tags = tagIds.Distinct().ToList();
        if (tags.Contains(Guid.Empty))
        {
            throw new ArgumentException("An empty id refers to nothing.", nameof(tagIds));
        }

        Title = title;
        CorrespondentId = correspondentId;
        DocumentTypeId = documentTypeId;
        DocumentDate = documentDate;
        Amount = amount;
        Summary = summary;
        TagIds = tags.AsReadOnly();
    }

    /// <summary>New title, or <c>null</c> to keep the current one.</summary>
    public string? Title { get; }

    public Guid? CorrespondentId { get; }

    public Guid? DocumentTypeId { get; }

    public DateOnly? DocumentDate { get; }

    public Money? Amount { get; }

    public string? Summary { get; }

    /// <summary>Distinct tag ids, in the given order.</summary>
    public IReadOnlyList<Guid> TagIds { get; }

    private static void RequireId(Guid? id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An empty id refers to nothing.", parameterName);
        }
    }
}
