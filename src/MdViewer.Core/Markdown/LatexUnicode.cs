using System.Text;

namespace MdViewer.Core.Markdown;

/// <summary>
/// Converts a LaTeX math expression to readable Unicode text.
///
/// MdViewer renders math in the normal text flow rather than typesetting it
/// (SPECIFICATION.md 9.2 defers a real typesetter), so the job here is to turn
/// the common notation people actually write — Greek letters, operators,
/// superscripts, subscripts, fractions and roots — into the Unicode characters
/// that stand for them. Anything unrecognised degrades to its literal text so a
/// formula is never swallowed.
/// </summary>
public static class LatexUnicode
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        // Lower-case Greek
        ["alpha"] = "α",
        ["beta"] = "β",
        ["gamma"] = "γ",
        ["delta"] = "δ",
        ["epsilon"] = "ε",
        ["varepsilon"] = "ε",
        ["zeta"] = "ζ",
        ["eta"] = "η",
        ["theta"] = "θ",
        ["vartheta"] = "ϑ",
        ["iota"] = "ι",
        ["kappa"] = "κ",
        ["lambda"] = "λ",
        ["mu"] = "μ",
        ["nu"] = "ν",
        ["xi"] = "ξ",
        ["pi"] = "π",
        ["rho"] = "ρ",
        ["sigma"] = "σ",
        ["tau"] = "τ",
        ["upsilon"] = "υ",
        ["phi"] = "φ",
        ["varphi"] = "φ",
        ["chi"] = "χ",
        ["psi"] = "ψ",
        ["omega"] = "ω",

        // Upper-case Greek
        ["Gamma"] = "Γ",
        ["Delta"] = "Δ",
        ["Theta"] = "Θ",
        ["Lambda"] = "Λ",
        ["Xi"] = "Ξ",
        ["Pi"] = "Π",
        ["Sigma"] = "Σ",
        ["Upsilon"] = "Υ",
        ["Phi"] = "Φ",
        ["Psi"] = "Ψ",
        ["Omega"] = "Ω",

        // Operators and relations
        ["pm"] = "±",
        ["mp"] = "∓",
        ["times"] = "×",
        ["div"] = "÷",
        ["cdot"] = "·",
        ["ast"] = "∗",
        ["star"] = "⋆",
        ["circ"] = "∘",
        ["bullet"] = "∙",
        ["leq"] = "≤",
        ["le"] = "≤",
        ["geq"] = "≥",
        ["ge"] = "≥",
        ["neq"] = "≠",
        ["ne"] = "≠",
        ["approx"] = "≈",
        ["sim"] = "∼",
        ["simeq"] = "≃",
        ["cong"] = "≅",
        ["equiv"] = "≡",
        ["propto"] = "∝",
        ["ll"] = "≪",
        ["gg"] = "≫",

        // Big operators
        ["sum"] = "∑",
        ["prod"] = "∏",
        ["coprod"] = "∐",
        ["int"] = "∫",
        ["iint"] = "∬",
        ["iiint"] = "∭",
        ["oint"] = "∮",

        // Sets and logic
        ["infty"] = "∞",
        ["partial"] = "∂",
        ["nabla"] = "∇",
        ["forall"] = "∀",
        ["exists"] = "∃",
        ["nexists"] = "∄",
        ["neg"] = "¬",
        ["lnot"] = "¬",
        ["land"] = "∧",
        ["wedge"] = "∧",
        ["lor"] = "∨",
        ["vee"] = "∨",
        ["in"] = "∈",
        ["notin"] = "∉",
        ["ni"] = "∋",
        ["subset"] = "⊂",
        ["subseteq"] = "⊆",
        ["supset"] = "⊃",
        ["supseteq"] = "⊇",
        ["cup"] = "∪",
        ["cap"] = "∩",
        ["setminus"] = "∖",
        ["emptyset"] = "∅",
        ["varnothing"] = "∅",
        ["aleph"] = "ℵ",

        // Arrows
        ["to"] = "→",
        ["rightarrow"] = "→",
        ["leftarrow"] = "←",
        ["leftrightarrow"] = "↔",
        ["Rightarrow"] = "⇒",
        ["Leftarrow"] = "⇐",
        ["Leftrightarrow"] = "⇔",
        ["mapsto"] = "↦",
        ["uparrow"] = "↑",
        ["downarrow"] = "↓",

        // Dots and misc
        ["ldots"] = "…",
        ["dots"] = "…",
        ["cdots"] = "⋯",
        ["vdots"] = "⋮",
        ["ddots"] = "⋱",
        ["angle"] = "∠",
        ["perp"] = "⊥",
        ["parallel"] = "∥",
        ["degree"] = "°",
        ["prime"] = "′",
        ["hbar"] = "ℏ",
        ["ell"] = "ℓ"
    };

    private static readonly Dictionary<char, char> Superscripts = new()
    {
        ['0'] = '⁰', ['1'] = '¹', ['2'] = '²', ['3'] = '³', ['4'] = '⁴',
        ['5'] = '⁵', ['6'] = '⁶', ['7'] = '⁷', ['8'] = '⁸', ['9'] = '⁹',
        ['+'] = '⁺', ['-'] = '⁻', ['='] = '⁼', ['('] = '⁽', [')'] = '⁾',
        ['a'] = 'ᵃ', ['b'] = 'ᵇ', ['c'] = 'ᶜ', ['d'] = 'ᵈ', ['e'] = 'ᵉ',
        ['f'] = 'ᶠ', ['g'] = 'ᵍ', ['h'] = 'ʰ', ['i'] = 'ⁱ', ['j'] = 'ʲ',
        ['k'] = 'ᵏ', ['l'] = 'ˡ', ['m'] = 'ᵐ', ['n'] = 'ⁿ', ['o'] = 'ᵒ',
        ['p'] = 'ᵖ', ['r'] = 'ʳ', ['s'] = 'ˢ', ['t'] = 'ᵗ', ['u'] = 'ᵘ',
        ['v'] = 'ᵛ', ['w'] = 'ʷ', ['x'] = 'ˣ', ['y'] = 'ʸ', ['z'] = 'ᶻ'
    };

    private static readonly Dictionary<char, char> Subscripts = new()
    {
        ['0'] = '₀', ['1'] = '₁', ['2'] = '₂', ['3'] = '₃', ['4'] = '₄',
        ['5'] = '₅', ['6'] = '₆', ['7'] = '₇', ['8'] = '₈', ['9'] = '₉',
        ['+'] = '₊', ['-'] = '₋', ['='] = '₌', ['('] = '₍', [')'] = '₎',
        ['a'] = 'ₐ', ['e'] = 'ₑ', ['h'] = 'ₕ', ['i'] = 'ᵢ', ['j'] = 'ⱼ',
        ['k'] = 'ₖ', ['l'] = 'ₗ', ['m'] = 'ₘ', ['n'] = 'ₙ', ['o'] = 'ₒ',
        ['p'] = 'ₚ', ['r'] = 'ᵣ', ['s'] = 'ₛ', ['t'] = 'ₜ', ['u'] = 'ᵤ',
        ['v'] = 'ᵥ', ['x'] = 'ₓ'
    };

    public static string Convert(string? latex)
    {
        if (string.IsNullOrEmpty(latex)) return string.Empty;

        var builder = new StringBuilder(latex.Length);
        var index = 0;
        AppendConverted(builder, latex, ref index, stopAtBrace: false);
        return builder.ToString().Trim();
    }

    private static void AppendConverted(StringBuilder builder, string latex, ref int index, bool stopAtBrace)
    {
        while (index < latex.Length)
        {
            var c = latex[index];

            if (c == '}')
            {
                if (stopAtBrace) return;
                index++;
                continue;
            }

            switch (c)
            {
                case '\\':
                    AppendCommand(builder, latex, ref index);
                    break;

                case '^':
                    index++;
                    AppendScript(builder, ReadGroup(latex, ref index), Superscripts, '^');
                    break;

                case '_':
                    index++;
                    AppendScript(builder, ReadGroup(latex, ref index), Subscripts, '_');
                    break;

                case '{':
                {
                    index++;
                    AppendConverted(builder, latex, ref index, stopAtBrace: true);
                    if (index < latex.Length && latex[index] == '}') index++;
                    break;
                }

                case '$':
                    index++;
                    break;

                default:
                    builder.Append(c);
                    index++;
                    break;
            }
        }
    }

    private static void AppendCommand(StringBuilder builder, string latex, ref int index)
    {
        // index points at the backslash.
        index++;

        if (index >= latex.Length)
        {
            return;
        }

        if (!char.IsLetter(latex[index]))
        {
            // Escaped punctuation and the thin-space commands \, \; \! \:
            var escaped = latex[index];
            index++;

            builder.Append(escaped switch
            {
                ',' or ';' or ':' => " ",
                '!' => string.Empty,
                '\\' => "\n",
                _ => escaped.ToString()
            });

            return;
        }

        var start = index;
        while (index < latex.Length && char.IsLetter(latex[index])) index++;
        var name = latex[start..index];

        switch (name)
        {
            case "frac" or "dfrac" or "tfrac":
            {
                var numerator = Convert(ReadGroup(latex, ref index));
                var denominator = Convert(ReadGroup(latex, ref index));
                builder.Append(Wrap(numerator)).Append('⁄').Append(Wrap(denominator));
                return;
            }

            case "sqrt":
            {
                var radicand = Convert(ReadGroup(latex, ref index));
                builder.Append('√').Append(Wrap(radicand));
                return;
            }

            case "text" or "mathrm" or "mathbf" or "mathit" or "operatorname":
                builder.Append(Convert(ReadGroup(latex, ref index)));
                return;

            case "left" or "right" or "big" or "Big" or "displaystyle":
                return;

            case "quad" or "qquad":
                builder.Append("  ");
                return;

            default:
                builder.Append(
                    Symbols.TryGetValue(name, out var symbol)
                        ? symbol
                        : name);
                return;
        }
    }

    private static void AppendScript(
        StringBuilder builder,
        string group,
        Dictionary<char, char> map,
        char marker)
    {
        var converted = Convert(group);
        if (converted.Length == 0) return;

        var scripted = new StringBuilder(converted.Length);

        foreach (var c in converted)
        {
            if (!map.TryGetValue(c, out var mapped))
            {
                // One unmappable character makes the whole script unreliable,
                // so fall back to the literal notation rather than a mix.
                builder.Append(marker).Append(Wrap(converted));
                return;
            }

            scripted.Append(mapped);
        }

        builder.Append(scripted);
    }

    /// <summary>
    /// Reads the argument after a command or a script marker: either a braced
    /// group, a single command, or a single character.
    /// </summary>
    private static string ReadGroup(string latex, ref int index)
    {
        while (index < latex.Length && latex[index] == ' ') index++;
        if (index >= latex.Length) return string.Empty;

        if (latex[index] == '{')
        {
            index++;
            var start = index;
            var depth = 1;

            while (index < latex.Length && depth > 0)
            {
                if (latex[index] == '{') depth++;
                else if (latex[index] == '}') depth--;

                if (depth > 0) index++;
            }

            var inner = latex[start..index];
            if (index < latex.Length) index++; // closing brace
            return inner;
        }

        if (latex[index] == '\\')
        {
            var start = index;
            index++;
            while (index < latex.Length && char.IsLetter(latex[index])) index++;
            return latex[start..index];
        }

        return latex[index++].ToString();
    }

    /// <summary>Parenthesises anything that is not already a single token.</summary>
    private static string Wrap(string value) =>
        value.Length <= 1 || IsBracketed(value) ? value : $"({value})";

    private static bool IsBracketed(string value) =>
        value.Length > 1 && value[0] == '(' && value[^1] == ')';
}
