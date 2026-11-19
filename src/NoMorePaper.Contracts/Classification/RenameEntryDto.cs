namespace NoMorePaper.Contracts.Classification;

/// <summary>Body for renaming a list entry.</summary>
/// <param name="Name">The new name, at most 200 characters.</param>
public sealed record RenameEntryDto(string Name);
