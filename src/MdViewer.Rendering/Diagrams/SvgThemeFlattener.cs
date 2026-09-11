using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Resolves CSS custom properties, <c>color-mix()</c> and <c>rem</c> units in
/// Mermaider's SVG output down to literal values, because Svg.Skia understands
/// <c>var()</c> but not <c>color-mix()</c> or <c>rem</c>.
///
/// Mermaider declares its palette in three places, none of them <c>:root</c>:
///   1. an inline <c>style</c> attribute on the root &lt;svg&gt; element, holding
///      --bg, --fg, --line, --accent, --muted, --surface and --border;
///   2. an <c>svg { ... }</c> rule inside &lt;style&gt;, deriving --_line, --_arrow,
///      --_node-fill and friends from (1), each with a color-mix() fallback;
///   3. --fs-xs / --fs-s / --fs-m / --fs-l, sized in rem.
/// Reading only <c>:root</c> yields an empty dictionary, and every
/// <c>var(--line, color-mix(...))</c> then collapses to its color-mix() fallback —
/// which Svg.Skia cannot parse, so the paint is dropped. A dropped <c>fill</c>
/// falls back to black and stays visible; a dropped <c>stroke</c> falls back to
/// none and the edge disappears.
/// </summary>
internal static class SvgThemeFlattener
{
    private const int MaxResolutionPasses = 12;

    /// <summary>Matches a top-level <c>:root { ... }</c>, <c>svg { ... }</c> or <c>* { ... }</c> rule.</summary>
    private static readonly Regex VariableRuleRegex = new(
        @"(?:^|[};])\s*(?::root|svg|\*)\s*\{(?<body>[^{}]*)\}",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Matches the inline style attribute on the root &lt;svg&gt; element.</summary>
    private static readonly Regex RootSvgStyleRegex = new(
        @"<svg\b[^>]*?\sstyle\s*=\s*(?<q>[""'])(?<body>.*?)\k<q>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DeclarationRegex = new(
        @"--(?<name>[\w-]+)\s*:\s*(?<value>[^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RemRegex = new(
        @"(?<value>-?(?:\d+\.?\d*|\d*\.\d+))rem\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Flatten(string svg, double baseFontSize)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            return svg;
        }

        var variables = ReadVariables(svg);
        ResolveVariableTable(variables);

        var current = ResolveVarUses(svg, variables);
        current = EvaluateColorMix(current);

        var remBase = baseFontSize > 0 ? baseFontSize : 15d;
        return ConvertRem(current, remBase);
    }

    // ---------------------------------------------------------------- variables

    /// <summary>
    /// Collects custom properties from the &lt;svg&gt; element's inline style and from
    /// every top-level <c>:root</c>/<c>svg</c>/<c>*</c> rule. The inline style wins,
    /// mirroring CSS specificity: it carries the caller's palette.
    /// </summary>
    private static Dictionary<string, string> ReadVariables(string svg)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Stop before the first @media block: its rules are conditional, and we
        // render an explicit palette rather than following the OS colour scheme.
        var media = svg.IndexOf("@media", StringComparison.OrdinalIgnoreCase);
        var ruleRegion = media < 0 ? svg : svg[..media];

        foreach (Match rule in VariableRuleRegex.Matches(ruleRegion))
        {
            ReadDeclarations(rule.Groups["body"].Value, result, overwrite: true);
        }

        var inline = RootSvgStyleRegex.Match(svg);
        if (inline.Success)
        {
            // Entity-decoded, because the style attribute is XML-escaped.
            var body = inline.Groups["body"].Value
                .Replace("&quot;", "\"", StringComparison.Ordinal)
                .Replace("&apos;", "'", StringComparison.Ordinal)
                .Replace("&amp;", "&", StringComparison.Ordinal);

            ReadDeclarations(body, result, overwrite: true);
        }

        return result;
    }

    private static void ReadDeclarations(string body, IDictionary<string, string> into, bool overwrite)
    {
        foreach (Match declaration in DeclarationRegex.Matches(body))
        {
            var name = declaration.Groups["name"].Value;
            var value = declaration.Groups["value"].Value.Trim();

            if (name.Length == 0 || value.Length == 0) continue;
            if (!overwrite && into.ContainsKey(name)) continue;

            into[name] = value;
        }
    }

    /// <summary>
    /// Resolves variable values against each other until stable, so
    /// <c>--_line: var(--line, ...)</c> becomes a literal colour before it is
    /// substituted into the document.
    /// </summary>
    private static void ResolveVariableTable(Dictionary<string, string> variables)
    {
        for (var pass = 0; pass < MaxResolutionPasses; pass++)
        {
            var changed = false;

            foreach (var name in variables.Keys.ToList())
            {
                var value = variables[name];

                // A variable must never resolve through itself.
                var withoutSelf = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
                withoutSelf.Remove(name);

                var resolved = EvaluateColorMix(ResolveVarUsesSinglePass(value, withoutSelf, out var substituted));

                if (substituted || !string.Equals(resolved, value, StringComparison.Ordinal))
                {
                    variables[name] = resolved;
                    changed = true;
                }
            }

            if (!changed) break;
        }
    }

    // ------------------------------------------------------------------- var()

    private static string ResolveVarUses(string svg, IReadOnlyDictionary<string, string> variables)
    {
        var current = svg;

        for (var i = 0; i < MaxResolutionPasses; i++)
        {
            current = ResolveVarUsesSinglePass(current, variables, out var changed);
            if (!changed) break;
        }

        return current;
    }

    private static string ResolveVarUsesSinglePass(
        string text,
        IReadOnlyDictionary<string, string> variables,
        out bool changed)
    {
        changed = false;

        var builder = new StringBuilder(text.Length);
        var cursor = 0;

        while (cursor < text.Length)
        {
            var start = text.IndexOf("var(", cursor, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                builder.Append(text, cursor, text.Length - cursor);
                break;
            }

            builder.Append(text, cursor, start - cursor);

            var end = FindClosingParenthesis(text, start + 3);
            if (end < 0)
            {
                builder.Append(text, start, text.Length - start);
                break;
            }

            var expression = text[(start + 4)..end];
            if (TryResolveVarExpression(expression, variables, out var replacement))
            {
                builder.Append(replacement);
                changed = true;
            }
            else
            {
                builder.Append(text, start, end - start + 1);
            }

            cursor = end + 1;
        }

        return builder.ToString();
    }

    private static bool TryResolveVarExpression(
        string expression,
        IReadOnlyDictionary<string, string> variables,
        out string replacement)
    {
        replacement = string.Empty;

        var comma = IndexOfTopLevelComma(expression);
        var primary = comma < 0 ? expression.Trim() : expression[..comma].Trim();
        var fallback = comma < 0 ? null : expression[(comma + 1)..].Trim();

        if (!primary.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        var name = primary[2..].Trim();
        if (name.Length > 0 && variables.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            replacement = value;
            return true;
        }

        if (string.IsNullOrWhiteSpace(fallback))
        {
            return false;
        }

        replacement = fallback;
        return true;
    }

    // -------------------------------------------------------------- color-mix()

    /// <summary>
    /// Evaluates <c>color-mix(in srgb, &lt;colour&gt; &lt;pct&gt;%, &lt;colour&gt;)</c>,
    /// the only form Mermaider emits. Unparseable mixes are left untouched.
    /// </summary>
    private static string EvaluateColorMix(string text)
    {
        if (text.IndexOf("color-mix(", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return text;
        }

        for (var pass = 0; pass < MaxResolutionPasses; pass++)
        {
            var changed = false;
            var builder = new StringBuilder(text.Length);
            var cursor = 0;

            while (cursor < text.Length)
            {
                var start = text.IndexOf("color-mix(", cursor, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    builder.Append(text, cursor, text.Length - cursor);
                    break;
                }

                builder.Append(text, cursor, start - cursor);

                var end = FindClosingParenthesis(text, start + "color-mix".Length);
                if (end < 0)
                {
                    builder.Append(text, start, text.Length - start);
                    break;
                }

                var expression = text[(start + "color-mix(".Length)..end];
                if (!expression.Contains("color-mix(", StringComparison.OrdinalIgnoreCase)
                    && TryEvaluateColorMix(expression, out var literal))
                {
                    builder.Append(literal);
                    changed = true;
                }
                else
                {
                    builder.Append(text, start, end - start + 1);
                }

                cursor = end + 1;
            }

            text = builder.ToString();
            if (!changed) break;
        }

        return text;
    }

    private static bool TryEvaluateColorMix(string expression, out string literal)
    {
        literal = string.Empty;

        var parts = SplitTopLevel(expression);
        if (parts.Count != 3) return false;
        if (!parts[0].Trim().StartsWith("in ", StringComparison.OrdinalIgnoreCase)) return false;

        // Only the sRGB space is emitted; anything else would need gamma handling.
        var space = parts[0].Trim()[3..].Trim();
        if (!space.Equals("srgb", StringComparison.OrdinalIgnoreCase)) return false;

        if (!TryParseColorWithPercentage(parts[1], out var first, out var firstWeight)) return false;
        if (!TryParseColorWithPercentage(parts[2], out var second, out var secondWeight)) return false;

        if (firstWeight is null && secondWeight is null) firstWeight = 50d;
        firstWeight ??= 100d - secondWeight!.Value;
        secondWeight ??= 100d - firstWeight.Value;

        var total = firstWeight.Value + secondWeight.Value;
        if (total <= 0) return false;

        var w1 = firstWeight.Value / total;
        var w2 = secondWeight.Value / total;

        // Premultiplied mixing, per css-color-5.
        var a = (first.A * w1) + (second.A * w2);
        double r, g, b;

        if (a <= 0)
        {
            r = g = b = 0;
        }
        else
        {
            r = ((first.R * first.A * w1) + (second.R * second.A * w2)) / a;
            g = ((first.G * first.A * w1) + (second.G * second.A * w2)) / a;
            b = ((first.B * first.A * w1) + (second.B * second.A * w2)) / a;
        }

        literal = a >= 0.999
            ? $"#{Clamp255(r):X2}{Clamp255(g):X2}{Clamp255(b):X2}"
            : $"#{Clamp255(r):X2}{Clamp255(g):X2}{Clamp255(b):X2}{Clamp255(a * 255d):X2}";

        return true;
    }

    private static bool TryParseColorWithPercentage(string part, out Rgba color, out double? percentage)
    {
        color = default;
        percentage = null;

        var text = part.Trim();
        if (text.Length == 0) return false;

        // "<colour> 32%" or "32% <colour>"
        var tokens = SplitOutsideParentheses(text);
        string? colorToken = null;

        foreach (var token in tokens)
        {
            if (token.EndsWith('%')
                && double.TryParse(token[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            {
                percentage = pct;
            }
            else
            {
                colorToken = colorToken is null ? token : colorToken + " " + token;
            }
        }

        return colorToken is not null && TryParseColor(colorToken, out color);
    }

    private readonly record struct Rgba(double R, double G, double B, double A);

    private static bool TryParseColor(string value, out Rgba color)
    {
        color = default;
        var text = value.Trim();

        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            switch (hex.Length)
            {
                case 3:
                case 4:
                {
                    if (!TryHexNibble(hex[0], out var r) || !TryHexNibble(hex[1], out var g) || !TryHexNibble(hex[2], out var b))
                        return false;
                    var a = 255d;
                    if (hex.Length == 4)
                    {
                        if (!TryHexNibble(hex[3], out var av)) return false;
                        a = av;
                    }
                    color = new Rgba(r, g, b, a / 255d);
                    return true;
                }
                case 6:
                case 8:
                {
                    if (!TryHexByte(hex, 0, out var r) || !TryHexByte(hex, 2, out var g) || !TryHexByte(hex, 4, out var b))
                        return false;
                    var a = 255d;
                    if (hex.Length == 8)
                    {
                        if (!TryHexByte(hex, 6, out var av)) return false;
                        a = av;
                    }
                    color = new Rgba(r, g, b, a / 255d);
                    return true;
                }
                default:
                    return false;
            }
        }

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = text.IndexOf('(');
            var close = text.LastIndexOf(')');
            if (open < 0 || close <= open) return false;

            var args = text[(open + 1)..close]
                .Replace('/', ',')
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (args.Length is < 3 or > 4) return false;

            var channels = new double[4];
            channels[3] = 1d;

            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                var isPercent = token.EndsWith('%');
                if (isPercent) token = token[..^1];

                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return false;

                channels[i] = i < 3
                    ? (isPercent ? number * 255d / 100d : number)
                    : (isPercent ? number / 100d : number);
            }

            color = new Rgba(channels[0], channels[1], channels[2], channels[3]);
            return true;
        }

        if (NamedColors.TryGetValue(text, out var named))
        {
            color = named;
            return true;
        }

        return false;
    }

    private static readonly Dictionary<string, Rgba> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new(0, 0, 0, 1),
        ["white"] = new(255, 255, 255, 1),
        ["transparent"] = new(0, 0, 0, 0),
        ["red"] = new(255, 0, 0, 1),
        ["green"] = new(0, 128, 0, 1),
        ["blue"] = new(0, 0, 255, 1),
        ["gray"] = new(128, 128, 128, 1),
        ["grey"] = new(128, 128, 128, 1),
        ["silver"] = new(192, 192, 192, 1),
        ["currentcolor"] = new(0, 0, 0, 1)
    };

    // ----------------------------------------------------------------- rem → px

    private static string ConvertRem(string text, double baseFontSize) =>
        RemRegex.Replace(text, m =>
        {
            if (!double.TryParse(m.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rem))
            {
                return m.Value;
            }

            return (rem * baseFontSize).ToString("0.###", CultureInfo.InvariantCulture) + "px";
        });

    // ------------------------------------------------------------------ helpers

    private static int Clamp255(double value) =>
        (int)Math.Round(Math.Clamp(value, 0d, 255d), MidpointRounding.AwayFromZero);

    private static bool TryHexNibble(char c, out double value)
    {
        value = 0;
        if (!Uri.IsHexDigit(c)) return false;
        var v = Convert.ToInt32(c.ToString(), 16);
        value = (v * 16) + v;
        return true;
    }

    private static bool TryHexByte(string hex, int index, out double value)
    {
        value = 0;
        if (!Uri.IsHexDigit(hex[index]) || !Uri.IsHexDigit(hex[index + 1])) return false;
        value = Convert.ToInt32(hex.Substring(index, 2), 16);
        return true;
    }

    private static int FindClosingParenthesis(string text, int openParenthesis)
    {
        var depth = 0;
        for (var i = openParenthesis; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '(') depth++;
            else if (ch == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }

    private static int IndexOfTopLevelComma(string value)
    {
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '(') depth++;
            else if (ch == ')') depth = Math.Max(0, depth - 1);
            else if (ch == ',' && depth == 0) return i;
        }

        return -1;
    }

    private static List<string> SplitTopLevel(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '(') depth++;
            else if (ch == ')') depth = Math.Max(0, depth - 1);
            else if (ch == ',' && depth == 0)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }

        parts.Add(value[start..]);
        return parts;
    }

    private static List<string> SplitOutsideParentheses(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i <= value.Length; i++)
        {
            if (i == value.Length)
            {
                if (i > start) parts.Add(value[start..i].Trim());
                break;
            }

            var ch = value[i];
            if (ch == '(') depth++;
            else if (ch == ')') depth = Math.Max(0, depth - 1);
            else if (char.IsWhiteSpace(ch) && depth == 0)
            {
                if (i > start) parts.Add(value[start..i].Trim());
                start = i + 1;
            }
        }

        parts.RemoveAll(string.IsNullOrWhiteSpace);
        return parts;
    }
}
