using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>The account of the request's sign-in cookie, after the session check of <see cref="SessionValidator"/>.</summary>
internal sealed class HttpCurrentAccount(IHttpContextAccessor accessor, IAccountStore accounts) : ICurrentAccount
{
    public Guid Id => AccountClaims.AccountId(accessor.HttpContext?.User) ?? throw NotSignedIn();

    public bool IsAdmin => Id != Guid.Empty && AccountClaims.IsAdministrator(accessor.HttpContext!.User);

    public Task<bool> ExistsAsync(CancellationToken cancellationToken) => accounts.ExistsAsync(Id, cancellationToken);

    // Only reached when an endpoint without sign-in calls a use case that needs one: a programming error.
    private static InvalidOperationException NotSignedIn() => new("Nobody is signed in.");
}
