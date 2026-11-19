using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Classification;

/// <summary>Plays the Worker's part in the database: recognized fields, duplicate hints, finished processing.</summary>
internal static class Recognition
{
    public static Task ClassifyAsync(this ApiFixture api, Guid documentId, DocumentFields fields) =>
        api.ChangeAsync(documentId, document => document.Classify(fields));

    public static Task MarkDuplicateAsync(this ApiFixture api, Guid documentId, Guid originalId) =>
        api.ChangeAsync(documentId, document => document.MarkPossibleDuplicate(originalId));

    public static Task CompleteAsync(this ApiFixture api, Guid documentId, string text) =>
        api.ChangeAsync(documentId, document =>
        {
            document.StartProcessing();
            document.Complete(text);
        });

    /// <summary>Adds an entry as <paramref name="client"/>'s account through the API.</summary>
    public static async Task<Guid> CreateEntryAsync(this HttpClient client, string kind, string name, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync($"/api/classification/{kind}", new CreateEntryDto(name), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EntryDto>(cancellationToken))!.Id;
    }

    /// <summary>Fields with only the given tags (or none), as recognition would set them.</summary>
    public static DocumentFields Fields(Guid? correspondentId = null, Guid? documentTypeId = null, params Guid[] tagIds) =>
        new(null, correspondentId, documentTypeId, new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), "Abschlag für November.", tagIds);

    private static async Task ChangeAsync(this ApiFixture api, Guid documentId, Action<Document> change)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.Documents.FindAsync([documentId]);
        change(document!);
        await db.SaveChangesAsync();
    }
}
