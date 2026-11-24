namespace NoMorePaper.Application.Editing;

/// <summary>The pages of a document for "Seiten bearbeiten", as the signed-in account sees them.</summary>
/// <param name="CanEdit">The account may edit the document (owner, editor).</param>
/// <param name="EditPending">An edit waits or runs; no new one is accepted until it is done.</param>
/// <param name="Pages">In order; empty while nothing is known about the pages.</param>
public sealed record DocumentPages(Guid Id, string Title, bool CanEdit, bool EditPending, IReadOnlyList<PageInfo> Pages);
