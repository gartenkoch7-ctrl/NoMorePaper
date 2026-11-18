namespace NoMorePaper.Infrastructure.Composition;

/// <summary>Configuration keys shared by Server, Worker and tests; other parts rely on these names.</summary>
public static class InfrastructureSettings
{
    /// <summary>Name under <c>ConnectionStrings</c> (env <c>ConnectionStrings__NoMorePaper</c>).</summary>
    public const string ConnectionStringName = "NoMorePaper";

    /// <summary>Folder of the Data Protection key ring (env <c>NoMorePaper__DataProtectionPath</c>).</summary>
    public const string DataProtectionPathKey = "NoMorePaper:DataProtectionPath";

    /// <summary>Root of the document storage (env <c>NoMorePaper__DataPath</c>); Server and Worker share it.</summary>
    public const string DataPathKey = "NoMorePaper:DataPath";

    /// <summary>Data root when <see cref="DataPathKey"/> is not set: the volume mounted in the containers.</summary>
    public const string DefaultDataPath = "/data";

    /// <summary>Shared by Server and Worker so each can unprotect what the other protected.</summary>
    public const string DataProtectionApplicationName = "NoMorePaper";
}
