using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

namespace MdViewer.Rendering.Diagrams;

internal static class SvgThemeFlattener
{
    private static readonly Regex RootRegex = new(
        @":root\s*\{(?<body>[^}]*)\}",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex VariableRegex = new(
        @"--(?<name>[\w-]+)\s*:\s*(?<value>[^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RemRegex = new(
        @"(?<value>-?(?:\d+\.?\d*|\d*\.\d+))rem",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Flatten(string svg, double baseFontSize)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            return svg;
        }

        var variables = ReadRootVariables(svg);
        var withResolvedVars = ResolveVarUses(svg, variables);
        var remBase = baseFontSize > 0 ? baseFontSize : 15d;

        return RemRegex.Replace(withResolvedVars, m =>
        {
            if (!double.TryParse(m.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rem))
            {
                return m.Value;
            }

            var px = rem * remBase;
            return px.ToString("0.###", CultureInfo.InvariantCulture) + "px";
        });
    }

    private static Dictionary<string, string> ReadRootVariables(string svg)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var root = RootRegex.Match(svg);
        if (!root.Success)
        {
            return result;
        }

        var body = root.Groups["body"].Value;
        foreach (Match variable in VariableRegex.Matches(body))
        {
            var name = variable.Groups["name"].Value;
            var value = variable.Groups["value"].Value.Trim();
            if (name.Length > 0 && value.Length > 0)
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static string ResolveVarUses(string svg, IReadOnlyDictionary<string, string> variables)
    {
        var current = svg;

        for (var i = 0; i < 8; i++)
        {
            current = ResolveVarUsesSinglePass(current, variables, out var changed);

            if (!changed)
            {
                break;
            }
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

        if (fallback.StartsWith("var(", StringComparison.OrdinalIgnoreCase)
            && fallback.EndsWith(')')
            && TryResolveVarExpression(fallback[4..^1], variables, out replacement))
        {
            return true;
        }

        replacement = fallback;
        return true;
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
}
