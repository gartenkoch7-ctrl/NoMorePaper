using NoMorePaper.Application.Editing;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>Page information in memory.</summary>
internal sealed class FakeDocumentPages : IDocumentPages
{
    public Dictionary<Guid, IReadOnlyList<PageInfo>> Pages { get; } = [];

    public Task<IReadOnlyList<PageInfo>> ListAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Pages.GetValueOrDefault(id) ?? []);
}
