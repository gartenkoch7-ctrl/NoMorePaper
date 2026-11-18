using Microsoft.AspNetCore.Identity;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>Translates Identity's results into the account problems of Application. Pure logic.</summary>
internal static class IdentityOutcome
{
    /// <summary>The problems behind a refused input; anything else is a fault, not an input error.</summary>
    /// <exception cref="InvalidOperationException">Identity refused for a reason no input can fix (e.g. a concurrent update).</exception>
    public static AccountProblem[] Problems(IdentityResult result)
    {
        var problems = result.Errors.Select(error => error.Code switch
        {
            nameof(IdentityErrorDescriber.DuplicateUserName) => AccountProblem.UserNameTaken,
            nameof(IdentityErrorDescriber.InvalidUserName) => AccountProblem.UserNameInvalid,
            nameof(IdentityErrorDescriber.PasswordTooShort) => AccountProblem.PasswordTooShort,
            nameof(IdentityErrorDescriber.PasswordMismatch) => AccountProblem.CurrentPasswordWrong,
            _ => (AccountProblem?)null,
        }).ToList();

        // Codes are fixed identifiers, never user input, so they may appear in the message.
        return problems.Count > 0 && problems.All(problem => problem is not null)
            ? [.. problems.Select(problem => problem!.Value).Distinct()]
            : throw new InvalidOperationException(
                "Identity refused the change: " + string.Join(", ", result.Errors.Select(error => error.Code)));
    }

    /// <exception cref="InvalidOperationException">The result is not a success.</exception>
    public static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity refused the change: " + string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }

    public static Account ToAccount(AccountUser user) =>
        new(user.Id, user.UserName ?? string.Empty, user.DisplayName, user.IsAdmin, user.SecurityStamp ?? string.Empty);
}
