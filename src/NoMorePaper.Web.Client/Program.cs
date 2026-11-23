using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NoMorePaper.Web.Client;
using NoMorePaper.Web.Client.Accounts;
using NoMorePaper.Web.Client.Camera;
using NoMorePaper.Web.Client.Classification;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Editing;
using NoMorePaper.Web.Client.Inputs;
using NoMorePaper.Web.Client.Localization;
using NoMorePaper.Web.Client.Trash;
using NoMorePaper.Web.Client.Viewer;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddSingleton<ICulturePreference, LocalStorageCulturePreference>();
builder.Services.AddSingleton<IUiCulture, BrowserUiCulture>();
builder.Services.AddScoped<SessionSignal>();
builder.Services.AddScoped(services => new HttpClient(new UnauthorizedHandler(services.GetRequiredService<SessionSignal>()))
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<AccountApi>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<DocumentApi>();
builder.Services.AddScoped<ClassificationApi>();
builder.Services.AddScoped<ReviewCounter>();
builder.Services.AddScoped<PreferencesApi>();
builder.Services.AddScoped<InputsApi>();
builder.Services.AddScoped<OwnerCheckApi>();
builder.Services.AddScoped<EditingApi>();
builder.Services.AddScoped<CropInterop>();
builder.Services.AddScoped<TrashApi>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ViewerInterop>();
builder.Services.AddScoped<ThumbnailRenderer>();
builder.Services.AddSingleton<ViewerPreferences>();
builder.Services.AddCamera();

var host = builder.Build();

// The culture must be set before RunAsync: Blazor loads the satellite resources of the culture
// in effect at startup, so changing the language later requires a reload.
var culture = await host.Services.GetRequiredService<ICulturePreference>().LoadAsync();
await host.Services.GetRequiredService<IUiCulture>().ApplyAsync(culture);

await host.RunAsync();
