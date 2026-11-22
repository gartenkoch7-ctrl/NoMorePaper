namespace NoMorePaper.Infrastructure.Documents;

/// <summary>A document type with its document count; property names equal the column aliases of the count query.</summary>
internal sealed class DocumentTypeCountRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }
}
