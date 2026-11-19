using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Security;
using NoMorePaper.Infrastructure.Documents.Pdf;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Security;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>The inputs of M4 (ADR-0024): what Server and Worker share, and the sources only the Worker runs.</summary>
public static class InputRegistration
{
    /// <summary>
    /// Stores of preferences, set-ups, state and seen items, the secret protector and the PDF composer;
    /// from <see cref="InfrastructureServiceCollectionExtensions.AddInfrastructure"/>, so Server and Worker both have them.
    /// </summary>
    public static IServiceCollection AddInputs(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAccountPreferencesStore, EfAccountPreferencesStore>();
        services.AddScoped<IInputConnections, EfInputConnections>();
        services.AddScoped<IInputStatusStore, EfInputStatusStore>();
        services.AddScoped<ISeenItems, EfSeenItems>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPdfComposer, PdfSharpComposer>();
        return services;
    }

    /// <summary>
    /// The background inputs the Worker's scheduler runs (<see cref="IInputSource"/>, scoped, at most one
    /// per kind). Only from the Worker's composition root. Each track of M4 adds its source here.
    /// </summary>
    public static IServiceCollection AddInputSources(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
