using NoMorePaper.Application.Recognition;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

public sealed class OllamaAnswerTests
{
    [Fact]
    public void Parse_ReadsEveryField()
    {
        var result = OllamaAnswer.Parse("""
            {"correspondent":" Stadtwerke München ","documentType":"Rechnung","summary":"Abschlag.","title":"Strom 2026",
             "documentDate":"2026-11-01","amount":1150.5,"currency":"CHF","tags":["Strom"," Energie "]}
            """);

        Assert.Equal(
            ("Stadtwerke München", "Rechnung", "Abschlag.", "Strom 2026", new DateOnly(2026, 11, 1)),
            (result.Correspondent, result.DocumentType, result.Summary, result.Title, result.DocumentDate));
        Assert.Equal((1150.5m, "CHF"), (result.Amount?.Value, result.Amount?.Currency));
        Assert.Equal(["Strom", "Energie"], result.Tags);
    }

    [Fact]
    public void Parse_LeavesEmpty_WhatIsNullBlankOrOfTheWrongType()
    {
        var result = OllamaAnswer.Parse("""
            {"correspondent":null,"documentType":"  ","summary":"","title":42,"documentDate":"2026-13-45","amount":null,
             "currency":null,"tags":[" ",7]}
            """);

        Assert.Equal((null, null, null, null), (result.Correspondent, result.DocumentType, result.Summary, result.Title));
        Assert.Null(result.DocumentDate);
        Assert.Null(result.Amount);
        Assert.Empty(result.Tags);
    }

    [Theory]
    [InlineData("\"currency\":null", "EUR")]
    [InlineData("\"currency\":\"eur\"", "EUR")]
    [InlineData("\"currency\":\"USD\"", "USD")]
    public void Parse_TakesEuro_WhenTheAmountHasNoValidCurrency(string currency, string expected)
    {
        var result = OllamaAnswer.Parse("{\"amount\":84.204999," + currency + "}");

        Assert.Equal((84.205m, expected), (result.Amount?.Value, result.Amount?.Currency));
    }

    [Fact]
    public void Parse_DropsAnAmountTooLargeToStore_AndKeepsTwoTags()
    {
        var result = OllamaAnswer.Parse("""{"amount":1e20,"tags":["a","b","c"]}""");

        Assert.Null(result.Amount);
        Assert.Equal(["a", "b"], result.Tags);
    }

    [Theory]
    [InlineData("Rechnung")]
    [InlineData("[1,2]")]
    [InlineData("{\"title\":")]
    public void Parse_Fails_WhenTheAnswerIsNoJsonObject(string content) =>
        Assert.Throws<RecognitionException>(() => OllamaAnswer.Parse(content));
}
