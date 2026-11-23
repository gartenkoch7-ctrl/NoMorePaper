namespace NoMorePaper.Application.Trash;

/// <summary>An entry of the trash as its owner's trash page lists it.</summary>
/// <param name="RestoreSwaps">
/// Restoring the edited version puts the edited version the document keeps now into the trash in its place.
/// </param>
/// <param name="DocumentInTrash">
/// The entry is a file of a document that lies in the trash itself; it can only come back with its document.
/// </param>
public sealed record TrashEntry(TrashItem Item, bool RestoreSwaps, bool DocumentInTrash);
