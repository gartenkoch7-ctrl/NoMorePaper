namespace NoMorePaper.Contracts.Editing;

/// <summary>The pages of an edited document in their new order; a page left out is deleted (<c>POST …/edit</c>).</summary>
public sealed record PagePlanDto(IReadOnlyList<PageStepDto> Steps);
