using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>The signed-in account of a test; switch it to act as somebody else, or remove it.</summary>
internal sealed class FakeCurrentAccount : ICurrentAccount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public bool IsAdmin { get; set; }

    /// <summary><c>false</c>: the account was removed while its cookie is still valid.</summary>
    public bool Exists { get; set; } = true;

    public Task<bool> ExistsAsync(CancellationToken cancellationToken) => Task.FromResult(Exists);
}
