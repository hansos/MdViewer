using Avalonia;
using Avalonia.Controls;

namespace MdViewer.Rendering;

/// <summary>
/// Applies per-tab zoom by overriding typography tokens with scaled values.
/// This keeps text crisp by changing font metrics rather than scaling pixels.
/// </summary>
public static class ZoomTypography
{
    /// <summary>
    /// The default type scale. Exposed so the settings layer can rescale the
    /// same set of tokens that zoom later multiplies.
    /// </summary>
    public static readonly (string Key, double BaseValue)[] Metrics =
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

    /// <summary>
    /// Prefix for the app-level base metrics written by the settings layer.
    /// Zoom multiplies whatever base the user configured, so a font-size
    /// preference and a per-tab zoom compose instead of overwriting each other.
    /// </summary>
    public const string BaseKeyPrefix = "AppBase";

    public static void Apply(Control target, double zoom)
    {
        ArgumentNullException.ThrowIfNull(target);

        var factor = Math.Clamp(zoom, 0.5, 3.0);
        var resources = target.Resources ??= new ResourceDictionary();

        foreach (var (key, fallback) in Metrics)
        {
            var baseValue = ResolveBase(key, fallback);
            resources[key] = Math.Round(baseValue * factor, 2);
        }
    }

    private static double ResolveBase(string key, double fallback)
    {
        var app = Application.Current;
        if (app is not null
            && app.Resources.TryGetResource(BaseKeyPrefix + key, null, out var value)
            && value is double configured
            && configured > 0)
        {
            return configured;
        }

        return fallback;
    }
}
