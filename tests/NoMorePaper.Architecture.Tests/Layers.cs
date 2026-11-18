using System.Reflection;

namespace NoMorePaper.Architecture.Tests;

/// <summary>
/// Assemblies under test, loaded by name so the tests do not depend on any type inside them.
/// All of them are project references of this test project and land in its output folder.
/// </summary>
internal static class Layers
{
    public const string Prefix = "NoMorePaper";

    public static Assembly Domain { get; } = Load("NoMorePaper.Domain");

    public static Assembly Application { get; } = Load("NoMorePaper.Application");

    public static Assembly Infrastructure { get; } = Load("NoMorePaper.Infrastructure");

    public static Assembly Contracts { get; } = Load("NoMorePaper.Contracts");

    public static Assembly Server { get; } = Load("NoMorePaper.Server");

    public static Assembly Worker { get; } = Load("NoMorePaper.Worker");

    public static Assembly WebClient { get; } = Load("NoMorePaper.Web.Client");

    public static IEnumerable<Assembly> All => [Domain, Application, Infrastructure, Contracts, Server, Worker, WebClient];

    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));
}
