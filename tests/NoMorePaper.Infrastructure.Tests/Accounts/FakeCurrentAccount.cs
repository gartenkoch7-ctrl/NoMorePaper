using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Tests.Accounts;

/// <summary>A signed-in account for use cases under test.</summary>
internal sealed class FakeCurrentAccount(Guid id, bool isAdmin = false) : ICurrentAccount
{
    public Guid Id => id;

    public bool IsAdmin => isAdmin;
}
