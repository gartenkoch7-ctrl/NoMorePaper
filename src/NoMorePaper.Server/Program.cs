using NoMorePaper.Server.Composition;
using NoMorePaper.Server.Documents;
using NoMorePaper.Server.Search;
using NoMorePaper.Server.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddServer();

var app = builder.Build();

// First: everything after it sees the scheme and host the browser used, not the proxy's.
app.UseForwardedHeaders();

// The host filter (AllowedHosts) already ran: only the configured names reach this point.
app.UseMiddleware<CrossSiteRequestGuard>();

// The WASM client (incl. _framework) is served by MapStaticAssets; UseBlazorFrameworkFiles must not
// be added on top of it, it breaks the _framework endpoints with HTTP 500 in .NET 10.
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapDocumentEndpoints();
app.MapSearchEndpoints();
app.MapFallbackToFile("index.html");

await app.RunAsync();

// Lets the integration tests start the real server (WebApplicationFactory<Program>).
public partial class Program;
