using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>The signed-in account of a test; switch it to act as somebody else.</summary>
internal sealed class FakeCurrentAccount : ICurrentAccount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public bool IsAdmin { get; set; }
}
