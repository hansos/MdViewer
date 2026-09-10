using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MdViewer.App.ViewModels;
using MdViewer.Rendering;

namespace MdViewer.App.Services;

/// <summary>
/// Pushes the typography settings (SPECIFICATION.md 5.13) into the application
/// resource dictionary. Font sizes are written as scaled base values under the
/// <see cref="ZoomTypography.BaseKeyPrefix"/> keys so per-tab zoom multiplies
/// the user's chosen size instead of replacing it.
/// </summary>
internal static class AppTypography
{
    private const double DefaultBodyFontSize = 15;
    private const double DefaultCodeFontSize = 13.5;
    private const double DefaultLineHeight = 1.65;

    public static void Apply(Application app, SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(settings);

        ApplyFontFamily(app, "BodyFontFamily", settings.BodyFontFamily);
        ApplyFontFamily(app, "CodeFontFamily", settings.MonospaceFontFamily);

        // Everything in the type scale moves with the body size, so headings
        // and tables stay proportional to the text the user actually reads.
        var bodyScale = Ratio(settings.BodyFontSize, DefaultBodyFontSize);
        var codeScale = Ratio(settings.MonospaceFontSize, DefaultCodeFontSize);
        var lineScale = Ratio(settings.LineHeight, DefaultLineHeight);

        foreach (var (key, baseValue) in ZoomTypography.Metrics)
        {
            var isCode = key.StartsWith("Code", StringComparison.Ordinal);
            var isLineHeight = key.EndsWith("LineHeight", StringComparison.Ordinal);

            var scale = isCode ? codeScale : bodyScale;
            if (isLineHeight) scale *= lineScale;

            app.Resources[ZoomTypography.BaseKeyPrefix + key] = Math.Round(baseValue * scale, 2);
        }
    }

    private static void ApplyFontFamily(Application app, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == SettingsViewModel.DefaultFont)
        {
            app.Resources.Remove(key);
            return;
        }

        app.Resources[key] = new FontFamily(value);
    }

    private static double Ratio(double value, double fallback) =>
        value > 0 ? value / fallback : 1.0;
}
