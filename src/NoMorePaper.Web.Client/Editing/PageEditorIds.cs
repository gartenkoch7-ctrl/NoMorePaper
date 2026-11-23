namespace NoMorePaper.Web.Client.Editing;

/// <summary>Element ids of the page editor; one place so focus targets cannot drift from the markup.</summary>
internal static class PageEditorIds
{
    public const string Heading1 = "pe-heading";
    public const string Save = "pe-save";
    public const string Discard = "pe-discard-button";
    public const string Append = "pe-append-button";
    public const string AppendHeading = "pe-append-heading";
    public const string Retry = "pe-retry";

    /// <summary>The H2 of the card of a stored page.</summary>
    public static string Heading(int sourcePage) => $"pe-h-{sourcePage}";

    /// <summary>
    /// A button of a card: <c>rl</c>, <c>rr</c>, <c>up</c>, <c>down</c>, <c>crop</c>, <c>auto</c>, <c>uncrop</c>, <c>del</c>,
    /// <c>split</c>; while cropping <c>crop-apply</c>, <c>crop-cancel</c>; after a failed search <c>crop-hand</c>.
    /// </summary>
    public static string Button(int sourcePage, string action) => $"pe-{action}-{sourcePage}";

    /// <summary>The handle of a corner (as seen) of the crop frame of a stored page.</summary>
    public static string Corner(int sourcePage, CropCorner corner) => $"pe-corner-{(int)corner}-{sourcePage}";

    /// <summary>The key hint of the crop frame; the handles are described by it.</summary>
    public static string CropKeys(int sourcePage) => $"pe-crop-keys-{sourcePage}";

    /// <summary>The "remove split point" button after the card at <paramref name="index"/>.</summary>
    public static string SplitRemove(int index) => $"pe-split-remove-{index}";
}
