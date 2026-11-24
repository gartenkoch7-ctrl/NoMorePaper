using NoMorePaper.Application.Editing;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Server.Editing;

/// <summary>Between the Contracts of scan editing and the Application and Domain types.</summary>
internal static class EditingMapping
{
    /// <returns>The plan, or <c>null</c> when a step is missing or its rotation unknown.</returns>
    public static PagePlan? ToDomain(PagePlanDto? plan)
    {
        if (plan?.Steps is not { } steps || steps.Any(step => step is null || !Enum.IsDefined(step.Rotation)))
        {
            return null;
        }

        return new PagePlan([.. steps.Select(step => new PageStep(step.SourcePage, (PageRotation)(int)step.Rotation, step.Replaced))]);
    }

    /// <returns>The split, or <c>null</c> when a part is not a valid page plan.</returns>
    public static SplitPlan? ToDomain(SplitPlanDto? plan)
    {
        if (plan?.Parts is not { } parts)
        {
            return null;
        }

        var mapped = parts.Select(ToDomain).ToList();
        return mapped.Any(part => part is null) ? null : new SplitPlan([.. mapped.OfType<PagePlan>()]);
    }

    /// <returns>The policy, or <c>null</c> when it is unknown.</returns>
    public static VersionPolicy? ToDomain(VersionPolicyDto policy) => policy switch
    {
        VersionPolicyDto.KeepBoth => VersionPolicy.KeepBoth,
        VersionPolicyDto.EditedOnly => VersionPolicy.EditedOnly,
        VersionPolicyDto.OriginalOnly => VersionPolicy.OriginalOnly,
        _ => null,
    };

    public static DocumentPagesDto ToDto(this DocumentPages pages) => new(
        pages.Id,
        pages.Title,
        pages.CanEdit,
        pages.EditPending,
        [.. pages.Pages.Select(page => new PageInfoDto(page.Page, page.Head, page.ProbablyBlank, page.HadTextLayer))]);
}
