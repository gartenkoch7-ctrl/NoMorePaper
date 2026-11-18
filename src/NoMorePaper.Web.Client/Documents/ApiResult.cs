namespace NoMorePaper.Web.Client.Documents;

/// <summary>Outcome of an API call: a value, or a localized message that is safe to show to the user.</summary>
/// <param name="Field">
/// For refused input: the request field the server named (e.g. <c>userName</c>), so the page can move
/// focus to the matching input; otherwise <c>null</c>.
/// </param>
public sealed record ApiResult<T>(T? Value, string? Error, string? Field = null)
{
    public bool IsSuccess => Error is null;
}
