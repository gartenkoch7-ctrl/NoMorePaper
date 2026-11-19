namespace NoMorePaper.Web.Client.Classification;

/// <summary>The three maintained lists.</summary>
public enum EntryKind
{
    Correspondents,
    DocumentTypes,
    Tags,
}

/// <summary>Where each list lives in the API.</summary>
public static class EntryKinds
{
    public static string Route(EntryKind kind) => kind switch
    {
        EntryKind.Correspondents => "correspondents",
        EntryKind.DocumentTypes => "document-types",
        _ => "tags",
    };
}
