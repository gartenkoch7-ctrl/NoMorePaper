namespace NoMorePaper.Application.Accounts;

/// <summary>Checks user name and password; issuing the session is the host's part (cookie).</summary>
public sealed class SignIn(IAccountStore accounts, TimeProvider clock)
{
    /// <summary>
    /// A refused sign-in is answered no sooner than <see cref="AccountRules.MinFailedSignInDuration"/>
    /// after it started. A wrong password for a known name also records the failure (a database
    /// write), an unknown name does not; padding both to the same minimum keeps the answer time from
    /// telling which names exist.
    /// </summary>
    public async Task<PasswordCheck> CheckAsync(string? userName, string? password, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var check = await accounts.CheckPasswordAsync(AccountInput.UserName(userName), password ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (check.Status == PasswordCheckStatus.Failed)
        {
            var remaining = AccountRules.MinFailedSignInDuration - clock.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, clock, cancellationToken).ConfigureAwait(false);
            }
        }

        return check;
    }
}
