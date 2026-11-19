namespace NoMorePaper.Domain.Documents;

/// <summary>A member the owner allowed to edit the fields of a document (table <c>document_editors</c>).</summary>
public sealed class DocumentEditor
{
    /// <param name="accountId">The member's account.</param>
    public DocumentEditor(Guid accountId) => AccountId = accountId;

    public Guid AccountId { get; }
}
