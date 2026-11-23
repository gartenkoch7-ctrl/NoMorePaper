using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Editing;

/// <summary>A split suggestion in "to review", accepted or dismissed (M4b, stack separation).</summary>
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
    public async Task Accept_QueuesTheSuggestedSplit_Once()
    {
        var splitter = new RecordingSplitRequester();
        await using var app = api.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ISplitRequester>(_ => splitter)));
        (await api.CreateMemberAsync("split-accept", Ct)).Client.Dispose();
        var (owner, _) = await app.SignInAsync("split-accept", ApiFixtureExtensions.MemberPassword, Ct);
        var stack = await owner.UploadPdfAsync("stapel.pdf", isPrivate: false, Ct);
        await SuggestAsync(stack);

        Assert.Equal(HttpStatusCode.Accepted, await PostAsync(owner, stack, "accept"));
        Assert.Equal(HttpStatusCode.Conflict, await PostAsync(owner, stack, "accept"));

        var (id, plan) = Assert.Single(splitter.Requests);
        Assert.Equal(stack, id);
        Assert.Equal(["1,2", "3", "4,6"], plan.Parts.Select(part => string.Join(',', part.Steps.Select(step => step.SourcePage))));
    }

    [Fact]
    public async Task Accept_IsAConflict_WhileTheSplitCannotBeQueued()
    {
        var splitter = new RecordingSplitRequester { Outcome = ChangeOutcome.Conflict };
        await using var app = api.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ISplitRequester>(_ => splitter)));
        (await api.CreateMemberAsync("split-busy", Ct)).Client.Dispose();
        var (owner, _) = await app.SignInAsync("split-busy", ApiFixtureExtensions.MemberPassword, Ct);
        var stack = await owner.UploadPdfAsync("stapel.pdf", isPrivate: false, Ct);
        await SuggestAsync(stack);

        Assert.Equal(HttpStatusCode.Conflict, await PostAsync(owner, stack, "accept"));

        // The suggestion stays, so it can be answered later.
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
}
