namespace NoMorePaper.Web.Client.Layout;

/// <summary>
/// Which of the five main navigation items a page belongs to, so the side navigation can mark it
/// with aria-current. Sub pages count for their main item (docs/ui/stitch/prompts.md §2).
/// </summary>
public static class NavSection
{
    /// <summary>Main item for a path relative to the base URI, or <c>null</c> (sign-in pages, not found).</summary>
    public static NavItem? For(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        var first = path.Split('/')[0];
        return first switch
        {
            "" => NavItem.Start,
            "hinzufuegen" or "hochladen" or "kamera" => NavItem.Add,
            "dokumente" or "suche" => NavItem.Documents,
            "zu-pruefen" => NavItem.Review,
            "einstellungen" or "konto" or "eingaenge" or "listen" or "mitglieder" => NavItem.Settings,
            _ => null,
        };
    }
}
