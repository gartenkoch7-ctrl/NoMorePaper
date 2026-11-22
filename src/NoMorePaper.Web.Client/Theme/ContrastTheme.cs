using MudBlazor;

namespace NoMorePaper.Web.Client.Theme;

/// <summary>
/// The only theme of the app: the Stitch design system "Obsidian Slate" (dark, monochrome),
/// approved by the main user as it is (docs/kb/oberflaeche.md, docs/kb/ui-prozess.md).
/// Values are taken over unchanged; wwwroot/css/base.css repeats them as CSS variables.
/// </summary>
public static class ContrastTheme
{
    private const string Canvas = "#131313";          // background
    private const string Paper = "#121212";           // cards, panels
    private const string Elevated = "#18181b";        // hover, popovers
    private const string Text = "#e5e2e1";            // on-surface: 14.4:1 on the canvas
    private const string TextSecondary = "#a1a1aa";   // 7.2:1
    private const string TextMuted = "#71717a";       // 3.8:1, disabled only
    private const string Primary = "#ffffff";
    private const string OnPrimary = "#0a0a0a";
    private const string Border = "#27272a";
    private const string BorderStrong = "#3f3f46";
    private const string Error = "#ffb4ab";
    private const string OnError = "#690005";

    private static readonly string[] _sans = ["Geist", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    /// <summary>Creates the theme. A new instance each call keeps callers from mutating a shared one.</summary>
    public static MudTheme Create() => new()
    {
        PaletteDark = CreatePalette(),
        Typography = CreateTypography(),
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "0.5rem" },
    };

    // The design is monochrome: every role colour except the error is white or grey, so meaning is
    // carried by symbol and text (AGENTS.md 2.5), never by hue.
    private static PaletteDark CreatePalette() => new()
    {
        Black = OnPrimary,
        White = Primary,
        Background = Canvas,
        BackgroundGray = Paper,
        Surface = Paper,
        AppbarBackground = Canvas,
        AppbarText = Text,
        DrawerBackground = "#1c1b1b",
        DrawerText = Text,
        DrawerIcon = Text,
        TextPrimary = Text,
        TextSecondary = TextSecondary,
        TextDisabled = TextMuted,
        ActionDefault = Text,
        ActionDisabled = TextMuted,
        ActionDisabledBackground = Elevated,
        Primary = Primary,
        PrimaryContrastText = OnPrimary,
        Secondary = "#c6c6cf",
        SecondaryContrastText = "#2f3037",
        Tertiary = Primary,
        TertiaryContrastText = OnPrimary,
        Info = "#c6c6cf",
        InfoContrastText = "#2f3037",
        Success = Primary,
        SuccessContrastText = OnPrimary,
        Warning = "#e2e2e2",
        WarningContrastText = OnPrimary,
        Error = Error,
        ErrorContrastText = OnError,
        Dark = Paper,
        DarkContrastText = Text,
        LinesDefault = Border,
        LinesInputs = Border,
        Divider = Border,
        DividerLight = Border,
        TableLines = Border,
        TableHover = Elevated,
        HoverOpacity = 0.06,
        OverlayDark = "rgba(0,0,0,0.85)",
        GrayLight = BorderStrong,
    };

    // Type scale of the design system (body-md 14 px, body-lg 16 px, headline-xl 40 px …), in rem
    // so browser and magnifier zoom keep working.
    private static Typography CreateTypography() => new()
    {
        Default = new DefaultTypography { FontFamily = _sans, FontSize = "0.875rem", LineHeight = "1.5715" },
        Body1 = new Body1Typography { FontFamily = _sans, FontSize = "1rem", LineHeight = "1.625", LetterSpacing = "-0.005em" },
        Body2 = new Body2Typography { FontFamily = _sans, FontSize = "0.875rem", LineHeight = "1.5715" },
        Button = new ButtonTypography
        {
            FontFamily = _sans,
            FontSize = "0.875rem",
            FontWeight = "500",
            TextTransform = "none",
        },
        Caption = new CaptionTypography { FontFamily = _sans, FontSize = "0.75rem", LineHeight = "1.5" },
        H1 = new H1Typography { FontFamily = _sans, FontSize = "2.5rem", FontWeight = "600", LineHeight = "1.2", LetterSpacing = "-0.03em" },
        H2 = new H2Typography { FontFamily = _sans, FontSize = "1.75rem", FontWeight = "600", LineHeight = "1.2857", LetterSpacing = "-0.02em" },
        H3 = new H3Typography { FontFamily = _sans, FontSize = "1.25rem", FontWeight = "500", LineHeight = "1.4", LetterSpacing = "-0.01em" },
        H4 = new H4Typography { FontFamily = _sans, FontSize = "1rem", FontWeight = "500", LineHeight = "1.5", LetterSpacing = "-0.005em" },
    };
}
