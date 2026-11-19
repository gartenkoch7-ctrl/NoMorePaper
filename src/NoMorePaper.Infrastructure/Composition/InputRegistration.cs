using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Security;
using NoMorePaper.Infrastructure.Documents.Pdf;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Inputs.ScannerFolder;
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
    /// The background inputs the Worker's scheduler runs. Only from the Worker's composition root. Each track
    /// of M4 adds its source here with <see cref="AddInputSource{TSource}"/>.
    /// </summary>
    public static IServiceCollection AddInputSources(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Scanner folder: only with a path. Checked when building the source, so a wrong path fails that
        // input only (Failed on the page "Inputs") and not the Worker. Files' state spans the runs.
        if (ScannerFolderSettings.IsEnabled(configuration))
        {
            services.AddSingleton(_ => ScannerFolderSettings.Read(configuration));
            services.AddSingleton<StableFiles>();
            services.AddScoped<AccountFolders>();
            services.AddScoped<ScannerFileIntake>();
            services.AddInputSource<ScannerFolderSource>(InputKind.ScannerFolder);
        }

        return services;
    }

    /// <summary>
    /// Registers the source of one input: scoped and keyed by <paramref name="kind"/>, so the scheduler builds
    /// only the source it runs, and announced by an <see cref="InputSourceRegistration"/>. One source per kind.
    /// </summary>
    public static IServiceCollection AddInputSource<TSource>(this IServiceCollection services, InputKind kind)
        where TSource : class, IInputSource
    {
        services.AddKeyedScoped<IInputSource, TSource>(kind);
        services.AddSingleton(new InputSourceRegistration(kind));
        return services;
    }
}
