using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.UI.Tests.Accounts;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>Answers for the three maintained lists.</summary>
internal static class ClassificationMocks
{
    public static async Task ListsAsync(
        AccountMocks mocks, IPage page, EntryDto[]? correspondents = null, EntryDto[]? types = null, EntryDto[]? tags = null)
    {
        await mocks.RespondAsync(page, "GET", "/api/classification/correspondents", 200, correspondents ?? ClassificationSamples.Correspondents());
        await mocks.RespondAsync(page, "GET", "/api/classification/document-types", 200, types ?? ClassificationSamples.Types());
        await mocks.RespondAsync(page, "GET", "/api/classification/tags", 200, tags ?? ClassificationSamples.Tags());
    }
}
