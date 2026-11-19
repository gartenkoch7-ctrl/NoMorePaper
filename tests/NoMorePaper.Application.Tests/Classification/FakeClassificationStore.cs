using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>Records the calls; renaming and merging answer <see cref="Change"/>.</summary>
internal sealed class FakeClassificationStore : IClassificationStore
{
    public List<string> Calls { get; } = [];

    public EntryChange Change { get; set; } = EntryChange.Changed;

    /// <summary>Actor or viewer of the last call.</summary>
    public Guid? LastAccount { get; private set; }

    /// <summary>The sources of the last <see cref="MergeAsync"/> call.</summary>
    public IReadOnlyCollection<Guid>? LastSources { get; private set; }

    public Task<IReadOnlyList<EntrySummary>> VisibleEntriesAsync(
        Guid viewerId, ClassificationKind kind, EntryAudience audience, CancellationToken cancellationToken)
    {
        Record(viewerId, $"list {kind} {audience}");
        return Task.FromResult<IReadOnlyList<EntrySummary>>([new EntrySummary(Guid.CreateVersion7(), "Stadtwerke")]);
    }

    public Task<EntrySummary> FindOrCreateAsync(
        Guid actorId, ClassificationKind kind, string name, EntryAudience audience, CancellationToken cancellationToken)
    {
        Record(actorId, $"find-or-create {kind} {name} {audience}");
        return Task.FromResult(new EntrySummary(Guid.CreateVersion7(), name));
    }

    public Task<EntryChange> RenameAsync(Guid actorId, ClassificationKind kind, Guid entryId, string name, CancellationToken cancellationToken)
    {
        Record(actorId, $"rename {kind} {name}");
        return Task.FromResult(Change);
    }

    public Task<EntryChange> MergeAsync(
        Guid actorId, ClassificationKind kind, Guid targetId, IReadOnlyCollection<Guid> sourceIds, CancellationToken cancellationToken)
    {
        Record(actorId, $"merge {kind}");
        LastSources = sourceIds;
        return Task.FromResult(Change);
    }

    private void Record(Guid account, string call)
    {
        LastAccount = account;
        Calls.Add(call);
    }
}
