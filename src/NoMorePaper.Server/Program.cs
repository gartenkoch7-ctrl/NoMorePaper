using NoMorePaper.Server.Composition;

var builder = WebApplication.CreateBuilder(args);
builder.AddServer();

var app = builder.Build();

// The WASM client is served as static assets; unknown routes fall back to the SPA shell.
app.UseBlazorFrameworkFiles();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

await app.RunAsync();
