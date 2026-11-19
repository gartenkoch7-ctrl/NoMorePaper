namespace NoMorePaper.Contracts.Classification;

/// <summary>
/// Body for adding a list entry (<c>POST /api/classification/{kind}</c>). Answers the entry the account
/// already sees under the same normalized name, or a new one.
/// </summary>
/// <param name="Name">At most 200 characters, with letters or digits.</param>
public sealed record CreateEntryDto(string Name);
