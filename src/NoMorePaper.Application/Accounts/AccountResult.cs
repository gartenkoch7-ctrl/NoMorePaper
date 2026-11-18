namespace NoMorePaper.Application.Accounts;

/// <summary>Outcome of an account operation without a value, and the factory of <see cref="AccountResult{T}"/>.</summary>
public class AccountResult
{
    private protected AccountResult(AccountStatus status, IReadOnlyList<AccountProblem> problems)
    {
        Status = status;
        Problems = problems;
    }

    public AccountStatus Status { get; }

    /// <summary>Empty unless <see cref="Status"/> is <see cref="AccountStatus.Invalid"/>.</summary>
    public IReadOnlyList<AccountProblem> Problems { get; }

    public bool Succeeded => Status == AccountStatus.Succeeded;

    public static AccountResult Success() => new(AccountStatus.Succeeded, []);

    public static AccountResult Fail(AccountStatus status) => new(RequireFailure(status), []);

    public static AccountResult Invalid(params AccountProblem[] problems) => new(AccountStatus.Invalid, RequireProblems(problems));

    public static AccountResult<T> Success<T>(T value) => new(AccountStatus.Succeeded, value, []);

    public static AccountResult<T> Fail<T>(AccountStatus status) => new(RequireFailure(status), default, []);

    public static AccountResult<T> Invalid<T>(params AccountProblem[] problems) => new(AccountStatus.Invalid, default, RequireProblems(problems));

    private static AccountStatus RequireFailure(AccountStatus status) =>
        status is AccountStatus.Succeeded or AccountStatus.Invalid
            ? throw new ArgumentOutOfRangeException(nameof(status), status, "Use Success or Invalid.")
            : status;

    private static AccountProblem[] RequireProblems(AccountProblem[] problems) =>
        problems is { Length: > 0 } ? problems : throw new ArgumentException("At least one problem.", nameof(problems));
}
