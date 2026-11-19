namespace NoMorePaper.Application.Documents;

/// <summary>Which earlier documents with exactly the same file stop the intake of a new one.</summary>
public enum DuplicateScope
{
    /// <summary>
    /// Every document the owner may see (own ones and shared ones): the browser upload, whose uploader is
    /// told about it (409) and can open it.
    /// </summary>
    Visible,

    /// <summary>
    /// Only the owner's own documents: background inputs, where nobody sees a refusal. Another account's
    /// shared document does not count, as it could become private later.
    /// </summary>
    Owned,
}
