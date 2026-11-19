namespace NoMorePaper.Web.Client.Documents;

/// <summary>Outcome of an API call: a value, or a localized message that is safe to show to the user.</summary>
/// <param name="Field">
/// For refused input: the request field the server named (e.g. <c>userName</c>), so the page can move
/// focus to the matching input; otherwise <c>null</c>.
/// </param>
/// <param name="ExistingId">
/// For an upload the server refused as a duplicate (409): the document that already holds the same file.
/// </param>
/// <param name="IsDuplicate">The upload was refused because the same file exists (the id may be unknown).</param>
/// <param name="IsConflict">
/// A change was refused with 409 because the data changed meanwhile (classification calls); the page
/// loads it again instead of keeping what it showed.
/// </param>
public sealed record ApiResult<T>(
    T? Value, string? Error, string? Field = null, Guid? ExistingId = null, bool IsDuplicate = false, bool IsConflict = false)
{
    public bool IsSuccess => Error is null;
}
