namespace NoMorePaper.Web.Client.Documents;

/// <summary>Where a document can be looked at; one place to change once the viewer exists.</summary>
public static class DocumentLinks
{
    public static string RowId(Guid id) => $"doc-{id:N}";

    // Until the viewer exists, a document is shown as its row in the list.
    public static string Of(Guid id) => $"dokumente#{RowId(id)}";
}
