namespace NoMorePaper.Application.Classification;

/// <summary>One entry of a maintained list as the store returns it; the Server maps it to the Contracts DTO.</summary>
public sealed record EntrySummary(Guid Id, string Name);
