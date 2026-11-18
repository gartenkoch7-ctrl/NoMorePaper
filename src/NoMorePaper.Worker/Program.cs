using Microsoft.AspNetCore.Builder;
using NoMorePaper.Worker.Composition;

var builder = WebApplication.CreateSlimBuilder(args);
builder.AddWorker();

var app = builder.Build();
app.MapHealthChecks("/health");

await app.RunAsync();
