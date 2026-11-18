namespace NoMorePaper.Web.Client.Documents;

/// <summary>Outcome of an API call: a value, or a localized message that is safe to show to the user.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
}
