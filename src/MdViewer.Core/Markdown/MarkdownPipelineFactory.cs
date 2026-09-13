using Markdig;

namespace MdViewer.Core.Markdown;

/// <summary>
/// Builds the one Markdig pipeline the whole application uses
/// (SPECIFICATION.md 5.1).
///
/// The pipeline is built once and shared: it is stateless and safe to parse
/// with from any thread. Extensions are listed explicitly rather than pulled in
/// with <c>UseAdvancedExtensions()</c>, because the specification names what is
/// on AND what is deliberately off, and a bundle would silently drift from that.
/// </summary>
public static class MarkdownPipelineFactory
{
    private static readonly Lazy<MarkdownPipeline> Shared = new(() => Build(), isThreadSafe: true);

    private static readonly Lazy<MarkdownPipeline> SharedSmart =
        new(() => Build(smartPunctuation: true), isThreadSafe: true);

    /// <summary>The shared, immutable pipeline.</summary>
    public static MarkdownPipeline Default => Shared.Value;

    /// <summary>
    /// The shared pipeline for the requested smart-punctuation setting
    /// (SPECIFICATION.md 5.1). Both variants are cached, so flipping the
    /// setting back and forth never rebuilds a pipeline.
    /// </summary>
    public static MarkdownPipeline For(bool smartPunctuation) =>
        smartPunctuation ? SharedSmart.Value : Shared.Value;

    public static MarkdownPipeline Build(bool smartPunctuation = false)
    {
        var builder = new MarkdownPipelineBuilder()
            // Tables
            .UsePipeTables()
            .UseGridTables()

            // Lists and text
            .UseTaskLists()
            .UseEmphasisExtras()      // strikethrough, sub/sup, ins, mark
            .UseDefinitionLists()
            .UseAbbreviations()
            .UseAutoLinks()
            .UseFootnotes()
            .UseMathematics()

            // Figures with captions: ^^^ ... ^^^ caption (5.2)
            .UseFigures()

            // Emoji shortcodes (:smile:) and text smileys (:)), both replaced
            // with the corresponding Unicode characters.
            .UseEmojiAndSmiley(enableSmileys: true)

            // Containers and GitHub alerts rendered as callouts
            .UseCustomContainers()
            .UseAlertBlocks()

            // Metadata, parsed but not rendered as body content (5.12)
            .UseYamlFrontMatter()

            // Heading anchors. MdViewer computes its own slugs for navigation
            // (see HeadingSlug) but this keeps Markdig's own link resolution
            // consistent with them.
            .UseAutoIdentifiers()

            // Source positions on every node. Everything downstream — search,
            // scroll restoration, the outline, and the v2 editor's scroll sync —
            // works in source-offset space, so this is not optional.
            .UsePreciseSourceLocation();

        if (smartPunctuation)
        {
            builder = builder.UseSmartyPants();
        }

        // Generic attributes: {: .some-class #some-id key=value }. Markdig
        // requires this to be the last extension registered, because it hooks
        // the parsers of every extension added before it.
        builder = builder.UseGenericAttributes();

        // NOT enabled, deliberately (SPECIFICATION.md 5.1):
        //   Bootstrap, JiraLinks, SelfPipeline, Globalization.

        return builder.Build();
    }
}
