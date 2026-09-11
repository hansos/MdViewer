using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CSharpMath.Avalonia;
using MdViewer.Core.Markdown;

namespace MdViewer.Rendering.Mathematics;

/// <summary>
/// Typesets LaTeX with CSharpMath (SPECIFICATION.md 5.1 mathematics).
///
/// CSharpMath is a managed TeX layout engine, so a formula gets real math
/// layout — a fraction stacks over its rule, an integral carries its limits
/// above and below the sign, scripts scale and shift correctly. Unicode
/// substitution cannot express any of that, so it survives only as the
/// fallback for expressions CSharpMath refuses.
///
/// All CSharpMath contact is confined to this class: the renderers stay free of
/// a third-party layout model, and replacing the engine later touches one file.
/// </summary>
internal static class MathTypesetter
{
    /// <summary>Font size used when the theme has not been resolved yet.</summary>
    private const float DefaultFontSize = 16f;

    /// <summary>
    /// Display math: its own centred line, set slightly larger than body copy
    /// the way a display equation is set in print.
    /// </summary>
    public static Control CreateBlock(string latex) => Create(latex, displayStyle: true);

    /// <summary>Inline math, sized to sit inside a line of running text.</summary>
    public static Control CreateInline(string latex) => Create(latex, displayStyle: false);

    private static Control Create(string latex, bool displayStyle)
    {
        var expression = (latex ?? string.Empty).Trim();
        if (expression.Length == 0)
        {
            return new Panel { IsVisible = false };
        }

        MathView view;

        try
        {
            view = new MathView
            {
                LaTeX = expression,
                DisplayErrorInline = false,
                FontSize = DefaultFontSize,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
        }
        catch
        {
            return BuildFallback(expression);
        }

        // CSharpMath reports a parse failure on the view rather than throwing,
        // so an unparseable formula is caught here and degrades to readable
        // text instead of rendering as an empty gap.
        if (!string.IsNullOrEmpty(view.ErrorMessage))
        {
            return BuildFallback(expression);
        }

        // Colour and size come from the theme tokens, but the view is not in a
        // visual tree yet and cannot resolve them. Doing it on attach also
        // means a theme switch or a zoom change is picked up without re-parsing
        // the document (SPECIFICATION.md 5.11, 6.1).
        view.AttachedToVisualTree += (_, _) => ApplyTheme(view, displayStyle);
        view.ActualThemeVariantChanged += (_, _) => ApplyTheme(view, displayStyle);

        // An embedded control is placed with its bottom edge on the text
        // baseline, so a tall formula reads as if it floats above the line. The
        // host reserves the space that puts the formula back on the line.
        return displayStyle ? view : new MathInlineHost(view, DefaultFontSize);
    }

    private static void ApplyTheme(MathView view, bool displayStyle)
    {
        if (TryFindResource(view, "TextPrimaryBrush") is ISolidColorBrush brush)
        {
            view.TextColor = brush.Color;
        }

        var bodySize = TryFindResource(view, "BodyFontSize") switch
        {
            double d => d,
            float f => f,
            _ => DefaultFontSize
        };

        // Display math is set a little larger than the surrounding prose;
        // inline math matches it so the line keeps an even colour.
        view.FontSize = (float)(displayStyle ? bodySize * 1.25 : bodySize);
    }

    private static object? TryFindResource(StyledElement element, string key) =>
        element.TryFindResource(key, element.ActualThemeVariant, out var value) ? value : null;

    /// <summary>
    /// What the reader sees when CSharpMath cannot lay the formula out: the
    /// Unicode approximation, which is still readable, rather than nothing.
    /// </summary>
    private static Control BuildFallback(string latex)
    {
        var text = new SelectableTextBlock { Text = LatexUnicode.Convert(latex) };
        text.Classes.Add("md-math-fallback");
        return text;
    }
}
