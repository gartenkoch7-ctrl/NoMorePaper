using Microsoft.Extensions.Configuration;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

public sealed class OllamaSettingsTests
{
    [Fact]
    public void Read_TakesTheValuesOfTheSpike_WhenNothingIsConfigured()
    {
        var settings = OllamaSettings.Read(Configuration());

        Assert.Equal(
            (new Uri("http://ollama:11434/"), "ministral-3:8b-instruct-2512-q4_K_M", TimeSpan.FromSeconds(300), 4),
            (settings.Endpoint, settings.Model, settings.Timeout, settings.NumThread));
    }

    [Theory]
    [InlineData("120", 120)]
    [InlineData("00:02:30", 150)]
    public void Read_TakesTheTimeout_InSecondsOrAsTimeSpan(string value, int seconds)
    {
        var settings = OllamaSettings.Read(Configuration((OllamaSettings.TimeoutKey, value)));

        Assert.Equal(TimeSpan.FromSeconds(seconds), settings.Timeout);
    }

    [Fact]
    public void Read_TakesEndpointModelAndThreads()
    {
        var settings = OllamaSettings.Read(Configuration(
            (OllamaSettings.EndpointKey, "http://127.0.0.1:11500"),
            (OllamaSettings.ModelKey, "qwen2.5:7b-instruct-q4_K_M"),
            (OllamaSettings.NumThreadKey, "0")));

        Assert.Equal(
            (new Uri("http://127.0.0.1:11500/"), "qwen2.5:7b-instruct-q4_K_M", 0),
            (settings.Endpoint, settings.Model, settings.NumThread));
    }

    [Theory]
    [InlineData(OllamaSettings.EndpointKey, "ollama:11434")]
    [InlineData(OllamaSettings.EndpointKey, "ftp://ollama")]
    [InlineData(OllamaSettings.TimeoutKey, "0")]
    [InlineData(OllamaSettings.TimeoutKey, "soon")]
    [InlineData(OllamaSettings.TimeoutKey, "1800")]
    [InlineData(OllamaSettings.NumThreadKey, "-1")]
    public void Read_RefusesInvalidValues_NamingTheKeyOnly(string key, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => OllamaSettings.Read(Configuration((key, value))));

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
