namespace NoMorePaper.Application.Accounts;

/// <summary>Why an input was refused; the Server reports these as fixed words per field.</summary>
public enum AccountProblem
{
    UserNameInvalid,
    UserNameTaken,
    DisplayNameInvalid,
    PasswordTooShort,
    CurrentPasswordWrong,
    CannotRemoveSelf,
}
