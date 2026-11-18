using MudBlazor;

namespace NoMorePaper.Web.Client.Theme;

/// <summary>
/// The only theme of the app: dark, high contrast, large type (AGENTS.md 2.5, ADR-0010).
/// Contrast ratios against the black background are noted per colour (WCAG AAA needs 7:1).
/// </summary>
public static class ContrastTheme
{
    private const string Black = "#000000";
    private const string White = "#FFFFFF";
    private const string Accent = "#FFD60A";      // 14.88:1
    private const string LightGray = "#E0E0E0";   // 15.91:1
    private const string MidGray = "#BDBDBD";     // 11.18:1
    private const string DisabledGray = "#B0B0B0"; // 9.68:1
    private const string Surface = "#121212";     // white text on it: 18.73:1

    private static readonly string[] _fontFamily =
        ["system-ui", "-apple-system", "Segoe UI", "Roboto", "Arial", "sans-serif"];

    // Base size 1.125rem = 18 px at default browser settings; rem keeps user zoom working.
    private const string BaseFontSize = "1.125rem";

    /// <summary>Creates the theme. A new instance each call keeps callers from mutating a shared one.</summary>
    public static MudTheme Create() => new()
    {
        PaletteDark = CreatePalette(),
        Typography = CreateTypography(),
    };

    private static PaletteDark CreatePalette() => new()
    {
        Black = Black,
        White = White,
        Background = Black,
        BackgroundGray = Surface,
        Surface = Surface,
        AppbarBackground = Black,
        AppbarText = White,
        DrawerBackground = Black,
        DrawerText = White,
        DrawerIcon = White,
        TextPrimary = White,
        TextSecondary = LightGray,
        TextDisabled = DisabledGray,
        ActionDefault = White,
        ActionDisabled = DisabledGray,
        ActionDisabledBackground = "#2A2A2A",
        Primary = Accent,
        PrimaryContrastText = Black,
        Secondary = "#9CC8FF",      // 12.12:1
        SecondaryContrastText = Black,
        Tertiary = "#80D8FF",       // 13.18:1
        TertiaryContrastText = Black,
        Info = "#80D8FF",           // 13.18:1
        InfoContrastText = Black,
        Success = "#69F0AE",        // 14.68:1
        SuccessContrastText = Black,
        Warning = "#FFB74D",        // 12.13:1
        WarningContrastText = Black,
        Error = "#FF8A80",          // 9.20:1
        ErrorContrastText = Black,
        Dark = Surface,
        DarkContrastText = White,
        LinesDefault = MidGray,
        LinesInputs = MidGray,
        Divider = MidGray,
        DividerLight = MidGray,
        TableLines = MidGray,
    };

    private static Typography CreateTypography() => new()
    {
        Default = new DefaultTypography { FontFamily = _fontFamily, FontSize = BaseFontSize, LineHeight = "1.6" },
        Body1 = new Body1Typography { FontFamily = _fontFamily, FontSize = BaseFontSize, LineHeight = "1.6" },
        Body2 = new Body2Typography { FontFamily = _fontFamily, FontSize = BaseFontSize, LineHeight = "1.6" },
        Button = new ButtonTypography
        {
            FontFamily = _fontFamily,
            FontSize = BaseFontSize,
            FontWeight = "700",
            TextTransform = "none", // all-caps text is harder to read for low-vision users
        },
        Caption = new CaptionTypography { FontFamily = _fontFamily, FontSize = BaseFontSize },
        H1 = new H1Typography { FontFamily = _fontFamily, FontSize = "2.5rem", FontWeight = "700", LineHeight = "1.2" },
        H2 = new H2Typography { FontFamily = _fontFamily, FontSize = "2rem", FontWeight = "700", LineHeight = "1.25" },
        H3 = new H3Typography { FontFamily = _fontFamily, FontSize = "1.5rem", FontWeight = "700", LineHeight = "1.3" },
    };
}
