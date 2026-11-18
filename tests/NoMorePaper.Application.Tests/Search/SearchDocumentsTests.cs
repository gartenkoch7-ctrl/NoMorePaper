using NoMorePaper.Application.Search;
using Xunit;

namespace NoMorePaper.Application.Tests.Search;

/// <summary>How a query and its limit reach the index; the search itself is tested against Postgres.</summary>
public sealed class SearchDocumentsTests
{
    private readonly RecordingIndex _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    [InlineData(" \0 \0 ")]
    public async Task Search_WithAnEmptyQuery_ReturnsNothing_WithoutAskingTheIndex(string? query)
    {
        Assert.Empty(await Search(query, 20));
        Assert.Empty(_index.Calls);
    }

    [Fact]
    public async Task Search_RemovesNulAndSurroundingSpace()
    {
        await Search("  Rech\0nung  ", 20);

        Assert.Equal(("Rechnung", 20), Assert.Single(_index.Calls));
    }

    [Fact]
    public async Task Search_CutsLongQueries()
    {
        await Search(new string('a', 500), 20);

        Assert.Equal(SearchDocuments.MaxQueryLength, Assert.Single(_index.Calls).Text.Length);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(5000, SearchDocuments.MaxLimit)]
    public async Task Search_ClampsTheLimit(int limit, int expected)
    {
        await Search("Rechnung", limit);

        Assert.Equal(expected, Assert.Single(_index.Calls).Limit);
    }

    private Task<IReadOnlyList<SearchHit>> Search(string? query, int limit) =>
        new SearchDocuments(_index).SearchAsync(query, limit, Ct);

    private sealed class RecordingIndex : ISearchIndex
    {
        public List<(string Text, int Limit)> Calls { get; } = [];

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string text, int limit, CancellationToken cancellationToken)
        {
            Calls.Add((text, limit));
            return Task.FromResult<IReadOnlyList<SearchHit>>([]);
        }
    }
}
