using Avalonia;
using Avalonia.Controls;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MdViewer.Rendering;

/// <summary>
/// Applies Markdig generic attributes — <c>{: .some-class #some-id key=value }</c>
/// — to the control a block renderer produced.
///
/// Classes become Avalonia style classes, so a theme can target authored
/// classes the same way it targets the built-in <c>md-*</c> ones. The id is
/// kept as an attached property rather than <see cref="StyledElement.Name"/>,
/// because names must be unique and valid identifiers while document ids are
/// neither guaranteed to be.
/// </summary>
public static class MarkdownAttributes
{
    /// <summary>The <c>#some-id</c> from the block's attribute list, if any.</summary>
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Id", typeof(MarkdownAttributes));

    public static void SetId(Control control, string? value) => control.SetValue(IdProperty, value);

    public static string? GetId(Control control) => control.GetValue(IdProperty);

    /// <summary>
    /// Copies the block's attribute list onto <paramref name="control"/>.
    /// Does nothing when the block carries no attributes, which is the common case.
    /// </summary>
    public static void Apply(Block block, Control control)
    {
        var attributes = block.TryGetAttributes();
        if (attributes is null) return;

        if (!string.IsNullOrEmpty(attributes.Id))
        {
            SetId(control, attributes.Id);
        }

        if (attributes.Classes is { Count: > 0 } classes)
        {
            foreach (var name in classes)
            {
                var styleClass = Normalize(name);
                if (styleClass is null || control.Classes.Contains(styleClass)) continue;

                control.Classes.Add(styleClass);
            }
        }
    }

    /// <summary>
    /// Avalonia rejects empty class names and treats a leading colon as a
    /// pseudo-class, so authored names that would throw are dropped instead.
    /// </summary>
    private static string? Normalize(string? name)
    {
        var trimmed = name?.Trim().TrimStart('.');
        if (string.IsNullOrEmpty(trimmed) || trimmed[0] == ':') return null;

        return trimmed;
    }
}
