namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>An invitation link (table <c>invitations</c>); only the hash of its token is kept.</summary>
public sealed class Invitation
{
    public required Guid Id { get; init; }

    /// <summary>SHA-256 of the token in the link, 64 hex characters.</summary>
    public required string TokenHash { get; init; }

    /// <summary>The administrator who created it.</summary>
    public required Guid CreatedBy { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>When a member was created with it; an invitation works once.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
