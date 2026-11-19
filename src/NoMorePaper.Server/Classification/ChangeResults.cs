using NoMorePaper.Application.Classification;
using NoMorePaper.Server.Accounts;

namespace NoMorePaper.Server.Classification;

/// <summary>
/// The HTTP answer to each <see cref="ChangeOutcome"/>, the same for every route of fields and lists:
/// 204, 404, 403, 400, 409, or 401 with the cookie cleared when the account was removed.
/// </summary>
internal static class ChangeResults
{
    public static async Task<IResult> ToResultAsync(this ChangeOutcome outcome, AccountSessions sessions, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        return outcome switch
        {
            ChangeOutcome.Changed => Results.NoContent(),
            ChangeOutcome.AccountGone => await sessions.EndRemovedAsync(context).ConfigureAwait(false),
            _ => Failure(outcome),
        };
    }

    /// <summary>The ProblemDetails of a refused change; not for <see cref="ChangeOutcome.Changed"/> or <see cref="ChangeOutcome.AccountGone"/>.</summary>
    public static IResult Failure(ChangeOutcome outcome) => outcome switch
    {
        ChangeOutcome.NotFound => NotFound(),
        ChangeOutcome.Forbidden => Results.Problem(title: "Not allowed.", statusCode: StatusCodes.Status403Forbidden),
        ChangeOutcome.Invalid => Results.Problem(title: "The request is invalid.", statusCode: StatusCodes.Status400BadRequest),
        ChangeOutcome.Conflict => Results.Problem(title: "Not possible in the current state.", statusCode: StatusCodes.Status409Conflict),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "No failure."),
    };

    public static IResult NotFound() => Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
}
