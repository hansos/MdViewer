using Markdig.Extensions.SmartyPants;

namespace MdViewer.Core.Markdown;

/// <summary>
/// Turns a smart-punctuation match into the character it stands for
/// (SPECIFICATION.md 5.1).
///
/// Markdig only classifies the match; its HTML renderer emits entities, which
/// are no use to a text flow or to plain text, so the mapping lives here and is
/// shared by the renderer and the plain-text flattener.
/// </summary>
public static class SmartPunctuation
{
    public static string ToText(SmartyPant pant)
    {
        ArgumentNullException.ThrowIfNull(pant);

        return pant.Type switch
        {
            SmartyPantType.Quote => "\u2019",
            SmartyPantType.LeftQuote => "\u2018",
            SmartyPantType.RightQuote => "\u2019",
            SmartyPantType.DoubleQuote => "\u201D",
            SmartyPantType.LeftDoubleQuote => "\u201C",
            SmartyPantType.RightDoubleQuote => "\u201D",
            SmartyPantType.LeftAngleQuote => "\u00AB",
            SmartyPantType.RightAngleQuote => "\u00BB",
            SmartyPantType.Ellipsis => "\u2026",
            SmartyPantType.Dash2 => "\u2013",
            SmartyPantType.Dash3 => "\u2014",
            _ => pant.ToString() ?? string.Empty,
        };
    }
}
