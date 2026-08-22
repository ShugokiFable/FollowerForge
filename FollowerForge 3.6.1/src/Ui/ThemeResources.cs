using Avalonia;
using Avalonia.Media;

namespace FollowerForge.Ui;

public sealed record ThemePalette(
    string Window,
    string Surface,
    string ElevatedSurface,
    string Border,
    string Text,
    string MutedText,
    string Accent,
    string AccentHover,
    string AccentPressed,
    string Success,
    string Info,
    string Warning,
    string Danger,
    /// <summary>Readable ink on top of solid Success/Info/Warning/Danger fills.</summary>
    string OnStatus,
    /// <summary>Translucent status fills for tinted badge chips (AARRGGBB, low alpha).</summary>
    string SuccessSoft,
    string InfoSoft,
    string WarningSoft,
    string DangerSoft,
    string Focus,
    string Selection,
    string Overlay);

public static class ThemeResources
{
    // Status hues are deliberately DISTINCT per theme. Pass 2 made chips follow the theme
    // token, but every token held nearly the same amber/red, so a theme switch never visibly
    // repainted statuses — the "yellow warning is frozen" bug the user reported.
    // Overlay is FULLY opaque: at 80% and even 95% the deck/palette dim let the sidebar's
    // bright status pills ghost through, which read as "chips overlay the yellow labels".
    public static ThemePalette Palette(UiTheme theme) => theme switch
    {
        UiTheme.ArcaneAmethyst => new(
            "#131018", "#1D1825", "#282031", "#493A59", "#F4EFF8", "#BDB0C8",
            "#A978E8", "#BB8EF1", "#8B5FC8", "#6FD19A", "#8FA6F2", "#E89A5C", "#F06E92",
            "#17101F", "#2E6FD19A", "#2E8FA6F2", "#2EE89A5C", "#2EF06E92",
            "#D4B2FF", "#443153", "#FF0B0810"),
        UiTheme.NordicFrost => new(
            "#0C141B", "#121F29", "#1A2B37", "#29495D", "#EDF8FF", "#A9C1CF",
            "#64C7F2", "#83D5F7", "#3CA5D1", "#62D49C", "#9FC2FF", "#F0D48A", "#F2827A",
            "#0A141B", "#2E62D49C", "#2E9FC2FF", "#2EF0D48A", "#2EF2827A",
            "#A8E4FF", "#1E4A5E", "#FF071017"),
        UiTheme.ForgeTeal => new(
            "#111716", "#18211F", "#21302D", "#36554F", "#EFF8F5", "#AEC4BE",
            "#43C7B0", "#63D6C1", "#2A9D8B", "#68D391", "#8FB8E8", "#D8C25C", "#F07A5C",
            "#0C1512", "#2E68D391", "#2E8FB8E8", "#2ED8C25C", "#2EF07A5C",
            "#91E8D8", "#24544C", "#FF09100F"),
        UiTheme.Light => new(
            "#F5F1E8", "#FFFDF8", "#FFFFFF", "#D4CCBC", "#26231E", "#6A6258",
            "#8A5D13", "#A87318", "#70490D", "#287A4B", "#2F6DA3", "#996C14", "#B43A3A",
            "#FFFDF8", "#24287A4B", "#242F6DA3", "#24996C14", "#24B43A3A",
            "#805400", "#F1E3C3", "#FF0F0D09"),
        _ => new(
            "#101114", "#181A20", "#22252D", "#383C47", "#F2F0EA", "#AAA9A4",
            "#D5A84B", "#E5BC69", "#B9872D", "#66CF91", "#8FAEE8", "#E0B05C", "#EB7474",
            "#161310", "#2E66CF91", "#2E8FAEE8", "#2EE0B05C", "#2EEB7474",
            "#F3CF7A", "#4A3A20", "#FF090A0C"),
    };

    public static void Apply(Application application, UiTheme theme)
    {
        // The Fluent theme variant must follow the palette too: hardcoded Dark left button
        // and glyph colors white-on-cream (invisible) in the Light theme.
        application.RequestedThemeVariant = theme == UiTheme.Light
            ? Avalonia.Styling.ThemeVariant.Light
            : Avalonia.Styling.ThemeVariant.Dark;
        var palette = Palette(theme);
        Put("WindowBrush", palette.Window);
        Put("SurfaceBrush", palette.Surface);
        Put("ElevatedSurfaceBrush", palette.ElevatedSurface);
        Put("BorderBrush", palette.Border);
        Put("TextBrush", palette.Text);
        Put("MutedTextBrush", palette.MutedText);
        Put("AccentBrush", palette.Accent);
        Put("AccentHoverBrush", palette.AccentHover);
        Put("AccentPressedBrush", palette.AccentPressed);
        Put("SuccessBrush", palette.Success);
        Put("InfoBrush", palette.Info);
        Put("WarningBrush", palette.Warning);
        Put("DangerBrush", palette.Danger);
        Put("OnStatusBrush", palette.OnStatus);
        Put("SuccessSoftBrush", palette.SuccessSoft);
        Put("InfoSoftBrush", palette.InfoSoft);
        Put("WarningSoftBrush", palette.WarningSoft);
        Put("DangerSoftBrush", palette.DangerSoft);
        Put("FocusBrush", palette.Focus);
        Put("SelectionBrush", palette.Selection);
        Put("OverlayBrush", palette.Overlay);
        application.Resources["Space8"] = 8d;
        application.Resources["Space12"] = 12d;
        application.Resources["Space16"] = 16d;
        application.Resources["Space20"] = 20d;
        application.Resources["Space24"] = 24d;
        application.Resources["Space32"] = 32d;
        application.Resources["ControlRadius"] = new CornerRadius(8);
        application.Resources["CardRadius"] = new CornerRadius(12);

        void Put(string key, string color) =>
            application.Resources[key] = new SolidColorBrush(Color.Parse(color));
    }
}
