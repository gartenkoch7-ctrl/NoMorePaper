namespace NoMorePaper.Contracts.Editing;

/// <summary>Appends the pages of <paramref name="AppendedId"/> to the open document (<c>POST …/append</c>).</summary>
public sealed record AppendRequestDto(Guid AppendedId);
