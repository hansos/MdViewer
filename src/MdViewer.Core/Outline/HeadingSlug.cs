using System.Text;

namespace MdViewer.Core.Outline;

/// <summary>
/// GitHub-compatible heading slugs, used for <c>#anchor</c> navigation
/// (SPECIFICATION.md 5.6, 5.8).
///
/// MdViewer computes these itself rather than reading Markdig's auto-identifier
/// attribute, so that anchor resolution has one definition that can be tested
/// directly and matches what people paste from GitHub.
/// </summary>
public static class HeadingSlug
{
    /// <summary>
    /// Lower-cases, drops everything that is not a letter, digit, space, hyphen
    /// or underscore, and turns runs of whitespace into single hyphens.
    /// </summary>
    public static string Create(string headingText)
    {
        if (string.IsNullOrWhiteSpace(headingText)) return string.Empty;

        var builder = new StringBuilder(headingText.Length);
        var pendingHyphen = false;

        foreach (var raw in headingText.Trim())
        {
            if (char.IsWhiteSpace(raw))
            {
                // Collapse runs, and never lead with a hyphen.
                pendingHyphen = builder.Length > 0;
                continue;
            }

            if (!char.IsLetterOrDigit(raw) && raw != '-' && raw != '_')
            {
                continue;
            }

            if (pendingHyphen)
            {
                builder.Append('-');
                pendingHyphen = false;
            }

            builder.Append(char.ToLowerInvariant(raw));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Makes a slug unique within a document by appending -1, -2, … exactly as
    /// GitHub does, so the second "Overview" heading is <c>#overview-1</c>.
    /// </summary>
    public static string MakeUnique(string slug, ISet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        if (string.IsNullOrEmpty(slug)) slug = "section";

        if (taken.Add(slug)) return slug;

        for (var suffix = 1; ; suffix++)
        {
            var candidate = $"{slug}-{suffix}";
            if (taken.Add(candidate)) return candidate;
        }
    }
}
