using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace MdViewer.Rendering;

/// <summary>
/// Binds a property to a theme token by key.
///
/// Renderers deal in style classes wherever a control supports them. Inlines
/// (<c>Run</c>, <c>Span</c>) do not support classes, so their colours and fonts
/// have to be set directly — and setting a literal colour there would put half
/// the theme outside the token set and break theme switching for exactly the
/// text people read most.
///
/// A dynamic-resource binding keeps the token discipline intact AND means a
/// theme switch repaints without re-parsing or re-rendering the document
/// (SPECIFICATION.md 5.11, 6.1).
/// </summary>
internal static class Themed
{
    public static T Apply<T>(this T target, AvaloniaProperty property, string resourceKey)
        where T : AvaloniaObject
    {
        target.Bind(property, new DynamicResourceExtension(resourceKey));
        return target;
    }

    /// <summary>Theme token keys, mirroring Themes/Tokens.axaml in MdViewer.App.</summary>
    public static class Keys
    {
        public const string TextPrimary = "TextPrimaryBrush";
        public const string TextSecondary = "TextSecondaryBrush";
        public const string TextMuted = "TextMutedBrush";
        public const string Link = "LinkBrush";
        public const string LinkBroken = "LinkBrokenBrush";
        public const string CodeInlineBackground = "CodeInlineBackgroundBrush";
        public const string CodeInlineForeground = "CodeInlineForegroundBrush";
        public const string FindMatch = "FindMatchBrush";
        public const string MonoFontFamily = "MonoFontFamily";
        public const string CodeFontSize = "CodeFontSize";
    }
}
