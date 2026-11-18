using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Server.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Hosting;

public sealed class ForwardedHeadersTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.1.20", false)]
    [InlineData("172.17.0.1", false)]
    public async Task ForwardedProto_IsTrustedOnlyFromTheProxyOnTheSameHost(string sender, bool expectedHttps)
    {
        var seen = new RequestSeenAfterTheServer();
        using var factory = api.WithWebHostBuilder(builder => builder
            .UseSetting("AllowedHosts", "localhost;127.0.0.1;nomorepaper.example.ts.net")
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new SenderFilter(IPAddress.Parse(sender), seen))));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "nomorepaper.example.ts.net");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(expectedHttps, seen.IsHttps);
        Assert.Equal(expectedHttps ? "nomorepaper.example.ts.net" : "localhost", seen.Host);
    }

    [Fact]
    public async Task ForwardedHost_MustBeAnAllowedHost_EvenFromTheProxy()
    {
        var seen = new RequestSeenAfterTheServer();
        using var factory = api.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new SenderFilter(IPAddress.Loopback, seen))));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Host", "evil.example");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal("localhost", seen.Host);
    }

    [Theory]
    [InlineData("172.18.0.1", true)]
    [InlineData("::ffff:172.18.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("192.168.1.20", false)]
    public async Task ForwardedProto_IsAlsoTrustedFromTheConfiguredNetworks(string sender, bool expectedHttps)
    {
        var seen = new RequestSeenAfterTheServer();
        using var factory = api.WithWebHostBuilder(builder => builder
            .UseSetting(ForwardedHeadersRegistration.TrustedProxiesKey, "172.16.0.0/12; 10.0.0.0/8")
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new SenderFilter(IPAddress.Parse(sender), seen))));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(expectedHttps, seen.IsHttps);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(" ", 0)]
    [InlineData("172.16.0.0/12", 1)]
    [InlineData("172.16.0.0/12, 10.0.0.0/8;", 2)]
    public void TrustedProxies_AreReadAsCidrList(string? setting, int expected) =>
        Assert.Equal(expected, ForwardedHeadersRegistration.TrustedNetworks(setting).Count());

    [Fact]
    public void TrustedProxies_RefuseWhatIsNotANetwork()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ForwardedHeadersRegistration.TrustedNetworks("172.16.0.0/12;docker").ToList());

        Assert.Contains("NoMorePaper:TrustedProxies", error.Message, StringComparison.Ordinal);
    }

    private sealed class RequestSeenAfterTheServer
    {
        public bool IsHttps { get; set; }

        public string? Host { get; set; }
    }

    /// <summary>Plays the sender: sets the remote address before the Server's pipeline, records the request after it.</summary>
    private sealed class SenderFilter(IPAddress sender, RequestSeenAfterTheServer seen) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                context.Connection.RemoteIpAddress = sender;
                await pipeline(context);
                seen.IsHttps = context.Request.IsHttps;
                seen.Host = context.Request.Host.Host;
            });
            next(app);
        };
    }
}
