using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Classification;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Editing;

/// <summary>A split suggestion in "to review", accepted (through the real split of track A) or dismissed.</summary>
public sealed class SplitSuggestionApiTests(ApiFixture api)
{
    private static readonly SplitSuggestion s_suggestion = new(
        [new PageRange(1, 2), new PageRange(3, 3), new PageRange(4, 6)], [5], [SplitReason.Blank, SplitReason.Salutation]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Review_ShowsTheSuggestion_AndDismissingEndsIt()
    {
        var (owner, _) = await api.CreateMemberAsync("split-owner", Ct);
        var (other, _) = await api.CreateMemberAsync("split-other", Ct);
        var stack = await owner.UploadPdfAsync("stapel.pdf", isPrivate: false, Ct);
        var secret = await owner.UploadPdfAsync("stapel-privat.pdf", isPrivate: true, Ct);
        await SuggestAsync(stack);
        await SuggestAsync(secret);

        var item = Assert.Single(await owner.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct) ?? [], found => found.Id == stack);
        Assert.Equal([new PageRangeDto(1, 2), new PageRangeDto(3, 3), new PageRangeDto(4, 6)], item.Split?.Parts ?? []);
        Assert.Equal([5], item.Split?.BlankPages ?? []);

        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(other, stack, "dismiss"));
        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(other, secret, "dismiss"));
        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(owner, stack, "dismiss"));
        Assert.Equal(HttpStatusCode.Conflict, await PostAsync(owner, stack, "dismiss"));
        Assert.DoesNotContain(await owner.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct) ?? [], found => found.Id == stack);
    }

    [Fact]
    public async Task Accept_QueuesTheSuggestedSplit_AndEndsTheSuggestion_Once()
    {
        var (owner, _) = await api.CreateMemberAsync("split-accept", Ct);
        var stack = await owner.UploadPdfAsync("stapel.pdf", isPrivate: false, Ct);
        await ProcessedAsync(stack);
        await SuggestAsync(stack);

        Assert.Equal(HttpStatusCode.Accepted, await PostAsync(owner, stack, "accept"));
        Assert.Equal(HttpStatusCode.Conflict, await PostAsync(owner, stack, "accept"));

        var (edit, job, suggestion) = await StateAsync(stack);
        Assert.Equal(EditKind.Split, edit?.Kind);
        var parts = EditDataJson.Parse(edit!.Data, EditKind.Split)?.Parts ?? [];
        Assert.Equal(["1,2", "3", "4,6"], parts.Select(part => string.Join(',', part.Select(step => step.SourcePage))));
        Assert.Equal(JobStage.Edit, job);
        Assert.Null(suggestion);
        Assert.DoesNotContain(await owner.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct) ?? [], found => found.Id == stack);
    }

    [Fact]
    public async Task Accept_IsAConflict_WhileTheSplitCannotBeQueued_AndKeepsTheSuggestion()
    {
        // Just uploaded: its OCR job waits, so the document is not done and nothing can be split yet.
        var (owner, _) = await api.CreateMemberAsync("split-busy", Ct);
        var stack = await owner.UploadPdfAsync("stapel.pdf", isPrivate: false, Ct);
        await SuggestAsync(stack);

        Assert.Equal(HttpStatusCode.Conflict, await PostAsync(owner, stack, "accept"));

        var (edit, job, suggestion) = await StateAsync(stack);
        Assert.Null(edit);
        Assert.Equal(JobStage.Ocr, job);
        Assert.Equal(s_suggestion.Parts, suggestion?.Parts);
        Assert.Contains(await owner.GetFromJsonAsync<ReviewItemDto[]>("/api/review", Ct) ?? [], found => found.Id == stack && found.Split is not null);
    }

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, Guid id, string answer)
    {
        using var response = await client.PostAsync($"/api/documents/{id}/split-suggestion/{answer}", null, Ct);
        return response.StatusCode;
    }

    // Plays the Worker's separation stage in the database.
    private async Task SuggestAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.Documents.FindAsync([id], Ct);
        document!.SuggestSplit(s_suggestion);
        await db.SaveChangesAsync(Ct);
    }

    // Plays the Worker: processed, and its job gone (OCR and recognition are over).
    private async Task ProcessedAsync(Guid id)
    {
        await api.CompleteAsync(id, "Stadtwerke München");
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ProcessingJobs.Where(job => job.DocumentId == id).ExecuteDeleteAsync(Ct);
    }

    private async Task<(DocumentEdit? Edit, JobStage? Job, SplitSuggestion? Suggestion)> StateAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var edit = await scope.ServiceProvider.GetRequiredService<IDocumentEdits>().GetAsync(id, Ct);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.ProcessingJobs.AsNoTracking()
            .Where(found => found.DocumentId == id)
            .Select(found => (JobStage?)found.Stage)
            .SingleOrDefaultAsync(Ct);
        var document = await db.Documents.AsNoTracking().SingleAsync(found => found.Id == id, Ct);
        return (edit, job, document.SplitSuggestion);
    }
}
