var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// The WASM client is served as static assets; unknown routes fall back to the SPA shell.
app.UseBlazorFrameworkFiles();
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();
