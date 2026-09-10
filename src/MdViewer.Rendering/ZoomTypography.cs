using Avalonia;
using Avalonia.Controls;

namespace MdViewer.Rendering;

/// <summary>
/// Applies per-tab zoom by overriding typography tokens with scaled values.
/// This keeps text crisp by changing font metrics rather than scaling pixels.
/// </summary>
public static class ZoomTypography
{
    private static readonly (string Key, double BaseValue)[] Metrics =
    [
        ("BodyFontSize", 15),
        ("BodyLineHeight", 25),
        ("CodeFontSize", 13.5),
        ("SmallFontSize", 12),

        ("H1FontSize", 30),
        ("H2FontSize", 23),
        ("H3FontSize", 19),
        ("H4FontSize", 16),
        ("H5FontSize", 15),
        ("H6FontSize", 14),

        ("H1LineHeight", 40),
        ("H2LineHeight", 32),
        ("H3LineHeight", 28),
        ("CodeLineHeight", 21),
        ("CodeLangFontSize", 11),
        ("TableFontSize", 14),
        ("TableLineHeight", 20)
    ];

    public static void Apply(Control target, double zoom)
    {
        ArgumentNullException.ThrowIfNull(target);

        var factor = Math.Clamp(zoom, 0.5, 3.0);
        var resources = target.Resources ??= new ResourceDictionary();

        foreach (var (key, baseValue) in Metrics)
        {
            resources[key] = Math.Round(baseValue * factor, 2);
        }
    }
}
