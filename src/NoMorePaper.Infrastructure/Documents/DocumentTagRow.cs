namespace NoMorePaper.Infrastructure.Documents;

/// <summary>A tag of a listed document; property names equal the column aliases of the tag query.</summary>
internal sealed class DocumentTagRow
{
    public Guid DocumentId { get; set; }

    public Guid TagId { get; set; }

    public string TagName { get; set; } = string.Empty;
}
