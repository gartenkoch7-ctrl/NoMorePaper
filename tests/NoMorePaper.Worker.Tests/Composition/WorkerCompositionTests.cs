using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Composition;
using NoMorePaper.Worker.Composition;
using Xunit;

namespace NoMorePaper.Worker.Tests.Composition;

/// <summary>
/// The Worker's composition root as the host builds it: every registration can be built (also those of
/// M4b's edit, separation and trash), every stage has its runner, and the hosted services can be made.
/// Nothing connects: the database and the tools are only reached when a job runs.
/// </summary>
public sealed class WorkerCompositionTests : IDisposable
{
    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-composition-tests-");

    public void Dispose() => _dataRoot.Delete(recursive: true);

    [Fact]
    public async Task TheWorker_BuildsItsServices_WithARunnerForEveryStage()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{InfrastructureSettings.ConnectionStringName}"] = "Host=localhost;Database=none;Username=none;Password=none",
            [InfrastructureSettings.DataPathKey] = _dataRoot.FullName,
            [InfrastructureSettings.DataProtectionPathKey] = Path.Combine(_dataRoot.FullName, "keys"),
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        builder.AddWorker();

        await using var app = builder.Build();

        var scope = app.Services.CreateAsyncScope();
        await using (scope)
        {
            Assert.All(Enum.GetValues<JobStage>(), stage => Assert.NotNull(scope.ServiceProvider.GetKeyedService<IStageRunner>(stage)));
        }

        Assert.NotEmpty(app.Services.GetServices<IHostedService>());
    }
}
