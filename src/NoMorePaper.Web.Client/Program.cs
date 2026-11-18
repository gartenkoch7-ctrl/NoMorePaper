using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NoMorePaper.Web.Client;
using NoMorePaper.Web.Client.Localization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddSingleton<ICulturePreference, LocalStorageCulturePreference>();
builder.Services.AddSingleton<IUiCulture, BrowserUiCulture>();

var host = builder.Build();

// The culture must be set before RunAsync: Blazor loads the satellite resources of the culture
// in effect at startup, so changing the language later requires a reload.
var culture = await host.Services.GetRequiredService<ICulturePreference>().LoadAsync();
await host.Services.GetRequiredService<IUiCulture>().ApplyAsync(culture);

await host.RunAsync();
