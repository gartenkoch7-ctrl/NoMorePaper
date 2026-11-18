namespace NoMorePaper.Application.Accounts;

/// <summary>
/// Cleans and checks what a person typed for a new account. The store checks the password and the
/// characters and uniqueness of the user name, so the rules live in one place each. Pure logic.
/// </summary>
public static class AccountInput
{
    /// <returns>The trimmed account, or the problems found; the password is passed on unchanged.</returns>
    public static AccountResult<NewAccount> Check(string? userName, string? displayName, string? password)
    {
        var name = (userName ?? string.Empty).Trim();
        var display = (displayName ?? string.Empty).Trim();
        var problems = new List<AccountProblem>();
        if (name.Length is 0 or > AccountRules.MaxUserNameLength)
        {
            problems.Add(AccountProblem.UserNameInvalid);
        }

        if (display.Length is 0 or > AccountRules.MaxDisplayNameLength || display.Any(char.IsControl))
        {
            problems.Add(AccountProblem.DisplayNameInvalid);
        }

        return problems.Count > 0
            ? AccountResult.Invalid<NewAccount>([.. problems])
            : AccountResult.Success<NewAccount>(new NewAccount(name, display, password ?? string.Empty));
    }

    /// <summary>A user name as typed at sign-in or emergency reset: trimmed, never <c>null</c>.</summary>
    public static string UserName(string? userName) => (userName ?? string.Empty).Trim();
}
