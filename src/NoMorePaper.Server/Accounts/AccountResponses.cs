using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// HTTP answers of the account API. Refused input is a 400 validation problem whose <c>errors</c> map
/// a request field to fixed words (<c>invalid</c>, <c>taken</c>, <c>too-short</c>, <c>wrong</c>,
/// <c>self</c>), which clients translate. Pure logic.
/// </summary>
internal static class AccountResponses
{
    public const string PasswordField = "password";
    public const string NewPasswordField = "newPassword";

    public static IResult Problem(int status, string title) => Results.Problem(title: title, statusCode: status);

    public static IResult SignInRequired() => Problem(StatusCodes.Status401Unauthorized, "Sign-in required.");

    public static IResult NotFound() => Problem(StatusCodes.Status404NotFound, "Not found.");

    /// <param name="passwordField">Request field that carries the new password.</param>
    public static IResult Failure(AccountResult result, string passwordField)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            AccountStatus.Invalid => Results.ValidationProblem(Errors(result.Problems, passwordField)),
            AccountStatus.NotFound => NotFound(),
            AccountStatus.Forbidden => Problem(StatusCodes.Status403Forbidden, "Only the administrator may do this."),
            AccountStatus.Conflict => Problem(StatusCodes.Status409Conflict, "The household is set up already."),
            AccountStatus.Unauthorized => Problem(StatusCodes.Status401Unauthorized, "Wrong user name or emergency code."),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Not a failure."),
        };
    }

    /// <summary>A member as others see it.</summary>
    public static AccountDto ToDto(this Account account) => new(account.Id, account.UserName, account.DisplayName, account.IsAdmin);

    /// <summary>The signed-in account as it sees itself, with the notice about an administrator's reset.</summary>
    public static AccountDto ToOwnDto(this Account account) => account.ToDto() with { AdminResetAt = account.AdminResetAt };

    private static Dictionary<string, string[]> Errors(IEnumerable<AccountProblem> problems, string passwordField) =>
        problems
            .Select(problem => problem switch
            {
                AccountProblem.UserNameInvalid => ("userName", "invalid"),
                AccountProblem.UserNameTaken => ("userName", "taken"),
                AccountProblem.DisplayNameInvalid => ("displayName", "invalid"),
                AccountProblem.PasswordTooShort => (passwordField, "too-short"),
                AccountProblem.CurrentPasswordWrong => ("currentPassword", "wrong"),
                AccountProblem.CannotRemoveSelf => ("id", "self"),
                _ => throw new ArgumentOutOfRangeException(nameof(problems), problem, "Unknown account problem."),
            })
            .GroupBy(error => error.Item1, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Item2).ToArray(), StringComparer.Ordinal);
}
