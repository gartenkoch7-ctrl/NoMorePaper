using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Web.Client.Documents;

namespace NoMorePaper.Web.Client.Camera;

/// <summary>Services of the camera page, registered by the composition root (Program.cs) with one call.</summary>
public static class CameraRegistration
{
    public static IServiceCollection AddCamera(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PageUploadApi>();
        services.AddScoped<CameraPreferences>();

        // Like the viewer: one bridge for the app, the page stops its camera when it is left. A transient
        // IAsyncDisposable would be kept by the app-long scope after every visit.
        services.AddScoped<CameraInterop>();

        // One announcement pace per visit of the page (holds no resources).
        services.AddTransient<CameraAnnouncer>();
        return services;
    }
}
