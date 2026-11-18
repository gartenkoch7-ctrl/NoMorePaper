namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>A reset link (table <c>password_reset_tokens</c>); deleted when used, only the hash of its token is kept.</summary>
public sealed class PasswordResetToken
{
    /// <summary>SHA-256 of the token in the link, 64 hex characters.</summary>
    public required string TokenHash { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
