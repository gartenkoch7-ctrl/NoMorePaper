namespace ICloudSpike.SelfTest;

/// <summary>
/// Stands in for the network: routes each request to the simulated Apple hosts and keeps a list of
/// the calls so tests can check which endpoints a run really used.
/// </summary>
internal sealed class FakeAppleServer : HttpMessageHandler
{
    private readonly FakeAuthHost _auth;

    public FakeAppleServer()
    {
        _auth = new FakeAuthHost(Account);
        Cloud = new FakeCloudHost(Account);
    }

    public FakeAppleAccount Account { get; } = new();

    public FakeCloudHost Cloud { get; }

    /// <summary>"METHOD host/path" per request, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>If set, every request fails like a broken connection.</summary>
    public bool Offline { get; set; }

    public int CallsTo(string host) => Calls.Count(call => call.Contains(" " + host + "/", StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("simulated: no route to host");
        }

        var uri = request.RequestUri!;
        Calls.Add($"{request.Method.Method} {uri.Host}{uri.AbsolutePath}");
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var response = uri.Host == "idmsa.apple.com" ? _auth.Handle(request, body) : Cloud.Handle(request, body);
        response.RequestMessage = request;
        return response;
    }
}
