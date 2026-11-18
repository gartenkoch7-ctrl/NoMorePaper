namespace NoMorePaper.Web.Client.Viewer;

/// <summary>What happened to the viewer's WebGL renderer; the page announces each one.</summary>
public enum RendererChange
{
    /// <summary>The browser took the WebGL context away; the page is rebuilt.</summary>
    Lost,

    /// <summary>The context is back and the page is drawn again with the shader.</summary>
    Restored,

    /// <summary>The context did not come back; the page is drawn with the CSS fallback.</summary>
    Fallback,
}
