using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace NoMorePaper.Architecture.Tests;

/// <summary>AGENTS.md 2.2 / ADR-0001: dependencies only point inwards, Web.Client only knows Contracts.</summary>
public sealed class LayerTests
{
    // Types that would let the Domain do IO; plain streams in memory stay allowed.
    private static readonly string[] s_domainForbiddenDependencies =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "System.Net.Http",
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.DriveInfo",
        "System.IO.Pipes",
    ];

    [Fact]
    public void Domain_ReferencesNoOtherProject() =>
        AssertProjectReferences(Layers.Domain);

    [Fact]
    public void Domain_HasNoFrameworkOrIoDependencies()
    {
        var result = Types.InAssembly(Layers.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(s_domainForbiddenDependencies)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_ReferencesOnlyDomain() =>
        AssertProjectReferences(Layers.Application, "NoMorePaper.Domain");

    [Fact]
    public void Application_HasNoPersistenceOrWebDependencies()
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "System.Net.Http")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Infrastructure_ReferencesOnlyApplicationAndDomain() =>
        AssertProjectReferences(Layers.Infrastructure, "NoMorePaper.Application", "NoMorePaper.Domain");

    [Fact]
    public void Contracts_ReferencesNoOtherProject() =>
        AssertProjectReferences(Layers.Contracts);

    [Fact]
    public void WebClient_ReferencesOnlyContracts() =>
        AssertProjectReferences(Layers.WebClient, "NoMorePaper.Contracts");

    [Fact]
    public void Worker_DoesNotReferenceServerWebClientOrContracts() =>
        AssertProjectReferences(Layers.Worker, "NoMorePaper.Infrastructure", "NoMorePaper.Application", "NoMorePaper.Domain");

    [Fact]
    public void Server_DoesNotReferenceWorker() =>
        AssertProjectReferences(
            Layers.Server,
            "NoMorePaper.Infrastructure",
            "NoMorePaper.Application",
            "NoMorePaper.Domain",
            "NoMorePaper.Contracts",
            "NoMorePaper.Web.Client");

    // The compiler only records references whose types are actually used, so this checks real use.
    private static void AssertProjectReferences(Assembly assembly, params string[] allowed)
    {
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(Layers.Prefix, StringComparison.Ordinal))
            .Except(allowed, StringComparer.Ordinal)
            .ToList();

        Assert.True(forbidden.Count == 0, $"{assembly.GetName().Name} must not reference: {string.Join(", ", forbidden)}");
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result)
    {
        var offenders = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful, "Layer violation in: " + string.Join(", ", offenders));
    }
}
