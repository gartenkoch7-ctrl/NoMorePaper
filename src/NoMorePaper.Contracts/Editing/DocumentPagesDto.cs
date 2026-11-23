namespace NoMorePaper.Contracts.Editing;

/// <summary>The pages of a document for "Seiten bearbeiten" (<c>GET /api/documents/{id}/pages</c>).</summary>
/// <param name="CanEdit">The signed-in account may edit it.</param>
/// <param name="EditPending">An edit waits or runs; no new one is accepted until it is done.</param>
/// <param name="Pages">In order; empty when nothing is known about the pages yet.</param>
public sealed record DocumentPagesDto(Guid Id, string Title, bool CanEdit, bool EditPending, IReadOnlyList<PageInfoDto> Pages);
