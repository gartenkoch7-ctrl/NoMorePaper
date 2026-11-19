using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Classification;
using NoMorePaper.Server.Composition;
using NoMorePaper.Server.Documents;
using NoMorePaper.Server.Inputs;
using NoMorePaper.Server.Search;
using NoMorePaper.Server.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddServer();

var app = builder.Build();

// First: everything after it sees the scheme and host the browser used, not the proxy's.
app.UseForwardedHeaders();

// The host filter (AllowedHosts) already ran: only the configured names reach this point.
app.UseMiddleware<CrossSiteRequestGuard>();

// Explicit, so they run after the two checks above and not where the host would add them by default.
app.UseAuthentication();
app.UseAuthorization();

// The WASM client (incl. _framework) is served by MapStaticAssets; UseBlazorFrameworkFiles must not
// be added on top of it, it breaks the _framework endpoints with HTTP 500 in .NET 10.
app.MapStaticAssets();
app.MapHealthChecks("/health");

// Every API route requires sign-in unless it opts out (setup, login, invitation and reset links).
var api = app.MapGroup("/api").RequireAuthorization();
api.MapAccountEndpoints();
api.MapDocumentEndpoints();
api.MapSearchEndpoints();
api.MapClassificationEndpoints();
api.MapPreferencesEndpoints();
api.MapOwnerCheckEndpoints();
api.MapInputStatusEndpoints();
app.MapFallbackToFile("index.html");

await app.RunAsync();

// Lets the integration tests start the real server (WebApplicationFactory<Program>).
public partial class Program;
