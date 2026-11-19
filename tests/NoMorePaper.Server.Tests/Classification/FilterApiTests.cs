using System.Net;
using System.Net.Http.Json;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Classification;

/// <summary>The filter query parameters of list and search, and the fields their rows carry.</summary>
public sealed class FilterApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAndSearch_FilterByTheQueryParameters_AndShowTheFields()
    {
        var (member, _) = await api.CreateMemberAsync("filter-member", Ct);
        var sender = await member.CreateEntryAsync("correspondents", "Stadtwerke Filterstadt", Ct);
        var tag = await member.CreateEntryAsync("tags", "Filterstrom", Ct);
        var matching = await member.UploadPdfAsync("treffer.pdf", isPrivate: true, Ct);
        var other = await member.UploadPdfAsync("anderes.pdf", isPrivate: true, Ct);
        await api.ClassifyAsync(matching, Recognition.Fields(sender, null, tag));
        await api.CompleteAsync(matching, "Jahresabrechnung Quokka");
        await api.CompleteAsync(other, "Jahresabrechnung Quokka");

        var query = $"correspondentId={sender}&from=2026-11-01&to=2026-11-30&tagIds={tag},{tag}&allTags=true&amountFrom=84.2&amountTo=84.20&isPrivate=true";
        var rows = await member.GetFromJsonAsync<DocumentSummaryDto[]>($"/api/documents?{query}", Ct);
        var hits = await member.GetFromJsonAsync<SearchHitDto[]>($"/api/search?q=Quokka&{query}", Ct);

        var row = Assert.Single(rows!);
        Assert.Equal((matching, "Stadtwerke Filterstadt", new DateOnly(2026, 11, 1)), (row.Id, row.Correspondent?.Name, row.DocumentDate));
        Assert.Equal((new MoneyDto(84.2m, "EUR"), DocumentRecognitionState.Automatic), (row.Amount, row.Recognition));
        Assert.Equal(matching, Assert.Single(hits!).Id);
        Assert.Equal("Stadtwerke Filterstadt", hits![0].Correspondent?.Name);
        Assert.Equal(2, (await member.GetFromJsonAsync<SearchHitDto[]>("/api/search?q=Quokka&correspondentId=", Ct))!.Length);
    }

    [Theory]
    [InlineData("correspondentId=abc")]
    [InlineData("from=01.11.2026")]
    [InlineData("tagIds=,")]
    [InlineData("tagIds=x,y")]
    [InlineData("amountFrom=84,20")]
    [InlineData("isPrivate=ja")]
    [InlineData("allTags=true&allTags=false")]
    public async Task UnreadableFilters_AreRefusedWith400(string query)
    {
        using var admin = api.CreateAdminClient();

        using var list = await admin.GetAsync($"/api/documents?{query}", Ct);
        using var search = await admin.GetAsync($"/api/search?q=a&{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, search.StatusCode);
    }

    [Fact]
    public async Task AFilterByAnotherAccountsPrivateEntry_FindsNothing()
    {
        var (anna, _) = await api.CreateMemberAsync("filter-anna", Ct);
        var (bob, _) = await api.CreateMemberAsync("filter-bob", Ct);
        var secret = await anna.CreateEntryAsync("tags", "Diagnose", Ct);
        var document = await anna.UploadPdfAsync("diagnose.pdf", isPrivate: true, Ct);
        await api.ClassifyAsync(document, Recognition.Fields(tagIds: secret));

        Assert.Empty((await bob.GetFromJsonAsync<DocumentSummaryDto[]>($"/api/documents?tagIds={secret}", Ct))!);
        Assert.Single((await anna.GetFromJsonAsync<DocumentSummaryDto[]>($"/api/documents?tagIds={secret}", Ct))!);
    }
}
