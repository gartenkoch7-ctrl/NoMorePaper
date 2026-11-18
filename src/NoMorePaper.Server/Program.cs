using NoMorePaper.Server.Composition;

var builder = WebApplication.CreateBuilder(args);
builder.AddServer();

var app = builder.Build();

// The WASM client (incl. _framework) is served by MapStaticAssets; UseBlazorFrameworkFiles must not
// be added on top of it, it breaks the _framework endpoints with HTTP 500 in .NET 10.
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

await app.RunAsync();
