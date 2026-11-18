namespace NoMorePaper.Application.Accounts;

/// <summary>
/// Outcome of an account operation; <see cref="Value"/> is set exactly when it succeeded. Created
/// through the factory methods of <see cref="AccountResult"/>.
/// </summary>
public sealed class AccountResult<T> : AccountResult
{
    internal AccountResult(AccountStatus status, T? value, IReadOnlyList<AccountProblem> problems, DateTimeOffset? lockedUntil = null)
        : base(status, problems, lockedUntil) => Value = value;

    public T? Value { get; }

    /// <summary>The same failure with another value type.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public AccountResult<TOther> Cast<TOther>() => Status switch
    {
        AccountStatus.Succeeded => throw new InvalidOperationException("Only failures can be cast."),
        AccountStatus.Invalid => Invalid<TOther>([.. Problems]),
        AccountStatus.LockedOut => Locked<TOther>(LockedUntil!.Value),
        _ => Fail<TOther>(Status),
    };
}
