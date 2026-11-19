using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Classification;

/// <summary>
/// Body for choosing the members who may edit a document's fields (owner only). Replaces the list;
/// required, so a request without it is refused instead of silently removing every editor.
/// </summary>
public sealed record SetEditorsRequestDto([property: JsonRequired] IReadOnlyList<Guid> EditorIds);
