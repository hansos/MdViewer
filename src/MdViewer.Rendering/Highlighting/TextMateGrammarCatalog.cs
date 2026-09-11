using TextMateSharp.Grammars;

namespace MdViewer.Rendering.Highlighting;

/// <summary>
/// Resolves grammar scope candidates from a fenced code info string
/// (SPECIFICATION.md 5.3).
///
/// Resolution order is strict:
/// 1) The raw info token itself (scope-style fences such as source.csharp)
/// 2) The info token interpreted as an extension
/// 3) The alias-extension table (js, sh, yml, ps1)
///
/// If nothing resolves, callers render plain text with no error.
/// </summary>
internal sealed class TextMateGrammarCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["js"] = ".js",
            ["sh"] = ".sh",
            ["yml"] = ".yml",
            ["ps1"] = ".ps1",
            ["csharp"] = ".cs",
            ["c#"] = ".cs",
            ["python"] = ".py",
            ["typescript"] = ".ts",
            ["javascript"] = ".js",
            ["shell"] = ".sh",
            ["bash"] = ".sh",
            ["zsh"] = ".sh",
            ["powershell"] = ".ps1",
            ["pwsh"] = ".ps1",
            ["yaml"] = ".yml",
            ["jsonc"] = ".json"
        };

    private readonly RegistryOptions _options;

    public TextMateGrammarCatalog(RegistryOptions options)
    {
        _options = options;
    }

    public IEnumerable<string> ResolveScopeCandidates(string infoString)
    {
        var info = NormalizeInfoToken(infoString);
        if (string.IsNullOrEmpty(info)) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (LooksLikeScopeName(info) && seen.Add(info))
        {
            yield return info;
        }

        if (TryGetScopeByExtension(info, out var fromInfoExtension) && seen.Add(fromInfoExtension))
        {
            yield return fromInfoExtension;
        }

        if (ExtensionAliases.TryGetValue(info, out var aliasExtension)
            && TryGetScopeByExtension(aliasExtension, out var fromAlias)
            && seen.Add(fromAlias))
        {
            yield return fromAlias;
        }
    }

    private bool TryGetScopeByExtension(string value, out string scope)
    {
        scope = string.Empty;

        var extension = NormalizeExtension(value);
        var language = _options.GetLanguageByExtension(extension);
        if (language is null) return false;

        var resolved = _options.GetScopeByLanguageId(language.Id);
        if (string.IsNullOrWhiteSpace(resolved)) return false;

        scope = resolved;
        return true;
    }

    private static string NormalizeInfoToken(string infoString)
    {
        if (string.IsNullOrWhiteSpace(infoString)) return string.Empty;

        var value = infoString.Trim();
        var split = value.IndexOfAny([' ', '\t']);
        return split >= 0 ? value[..split] : value;
    }

    private static string NormalizeExtension(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var trimmed = value.Trim();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private static bool LooksLikeScopeName(string value)
    {
        return value.Contains('.') && !value.StartsWith('.');
    }
}
