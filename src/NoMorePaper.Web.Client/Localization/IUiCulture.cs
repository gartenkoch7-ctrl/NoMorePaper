namespace NoMorePaper.Web.Client.Localization;

/// <summary>Switches the running app (texts, formats, document language) to a UI language.</summary>
public interface IUiCulture
{
    /// <summary>The language currently in effect, always one of <see cref="SupportedCultures.All"/>.</summary>
    string Current { get; }

    /// <summary>
    /// Applies <paramref name="culture"/> to .NET and to <c>&lt;html lang&gt;</c>, so screen readers
    /// use the right voice. Must run before the host starts, because satellite resources are
    /// loaded for the culture in effect at startup.
    /// </summary>
    Task ApplyAsync(string culture);
}
