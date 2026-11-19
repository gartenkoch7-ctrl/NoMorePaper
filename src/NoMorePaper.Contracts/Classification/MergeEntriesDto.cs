using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Classification;

/// <summary>Body for merging list entries: the documents of <paramref name="SourceIds"/> move to <paramref name="TargetId"/>, the sources are deleted.</summary>
public sealed record MergeEntriesDto([property: JsonRequired] Guid TargetId, [property: JsonRequired] IReadOnlyList<Guid> SourceIds);
