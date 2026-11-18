using NoMorePaper.Server.Composition;
using NoMorePaper.Server.Documents;
using NoMorePaper.Server.Search;

var builder = WebApplication.CreateBuilder(args);
builder.AddServer();

var app = builder.Build();

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
