using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Separation;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Recognition;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>Registers recognition (local LLM, examples, content duplicates); only the Worker recognizes.</summary>
public static class RecognitionRegistration
{
    // A changed address of the service (container restarted) is picked up after this time.
    private static readonly TimeSpan s_connectionLifetime = TimeSpan.FromMinutes(5);

    /// <remarks>Configuration is read when the recognizer is first resolved, like in <c>AddInfrastructure</c>.</remarks>
    public static IServiceCollection AddRecognition(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(_ => OllamaSettings.Read(configuration));

        // One request to Ollama at a time, for recognition and the mail check (ADR-0024).
        services.AddSingleton<LlmGate>();
        services.AddSingleton<IRecognizer>(provider =>
        {
            var settings = provider.GetRequiredService<OllamaSettings>();
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = s_connectionLifetime })
            {
                BaseAddress = settings.Endpoint,

                // Each call sets its own limit; downloading the model may take long.
                Timeout = Timeout.InfiniteTimeSpan,
            };
            return new OllamaRecognizer(
                http,
                settings,
                provider.GetRequiredService<LlmGate>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<OllamaRecognizer>>());
        });
        services.AddSingleton<IMailAssessor>(provider =>
        {
            var settings = provider.GetRequiredService<OllamaSettings>();
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = s_connectionLifetime })
            {
                BaseAddress = settings.Endpoint,
                Timeout = Timeout.InfiniteTimeSpan,
            };
            return new OllamaMailAssessor(
                http,
                settings,
                provider.GetRequiredService<LlmGate>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<OllamaMailAssessor>>());
        });
        services.AddScoped<IRecognitionExamples, EfRecognitionExamples>();
        services.AddScoped<IDuplicateFinder, PostgresDuplicateFinder>();

        // Stack separation (M4b): the same Ollama behind the same gate, and its end in the queue.
        services.AddSingleton<ISeparationAssessor>(provider =>
        {
            var settings = provider.GetRequiredService<OllamaSettings>();
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = s_connectionLifetime })
            {
                BaseAddress = settings.Endpoint,
                Timeout = Timeout.InfiniteTimeSpan,
            };
            return new OllamaSeparationAssessor(
                http,
                settings,
                provider.GetRequiredService<LlmGate>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<OllamaSeparationAssessor>>());
        });
        services.AddScoped<ISeparationQueue, JobQueue>();
        return services;
    }
}
