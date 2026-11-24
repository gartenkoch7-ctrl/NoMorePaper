namespace NoMorePaper.Web.Client.Documents;

/// <summary>Where a document can be looked at or downloaded; one place for every link to it.</summary>
public static class DocumentLinks
{
    public static string RowId(Guid id) => $"doc-{id:N}";

    /// <summary>The document's row in the list, where its processing state is visible (used after an upload).</summary>
    public static string Of(Guid id) => $"dokumente#{RowId(id)}";

    /// <summary>The viewer page of the document.</summary>
    public static string Viewer(Guid id) => $"dokumente/{id}";

    /// <summary>The unchanged original file as uploaded.</summary>
    public static string Original(Guid id) => $"api/documents/{id}/files/original";

    /// <summary>The PDF/A made by the processing; exists once the document is done.</summary>
    public static string Archive(Guid id) => $"api/documents/{id}/files/archive";

    /// <summary>The newest edited version (PDF); exists once the document was edited.</summary>
    public static string Edited(Guid id) => $"api/documents/{id}/files/edited";

    /// <summary>The page editor; with <paramref name="split"/> the split points of the suggestion are set.</summary>
    public static string PageEditor(Guid id, bool split = false) => split ? $"dokumente/{id}/seiten?trennen=1" : $"dokumente/{id}/seiten";
}
