using Avalonia.Controls;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Resolves a block to the renderer that handles it (SPECIFICATION.md 5.2).
///
/// Registration order is priority order, so a more specific renderer added
/// later — diagrams, custom containers — can be inserted ahead of a general one
/// without editing anything that already works.
/// </summary>
public sealed class BlockRendererRegistry
{
    private readonly List<IBlockRenderer> _renderers = new();

    public static BlockRendererRegistry CreateDefault() => new BlockRendererRegistry()
        .Add(new HeadingBlockRenderer())
        .Add(new ParagraphBlockRenderer())
        .Add(new CodeBlockRenderer())
        .Add(new TableBlockRenderer())
        .Add(new ListBlockRenderer())
        .Add(new QuoteBlockRenderer())
        .Add(new ThematicBreakRenderer())
        .Add(new HtmlBlockRenderer())
        .Add(new IgnoredBlockRenderer());

    public BlockRendererRegistry Add(IBlockRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers.Add(renderer);
        return this;
    }

    /// <summary>Inserts ahead of everything already registered.</summary>
    public BlockRendererRegistry Prepend(IBlockRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers.Insert(0, renderer);
        return this;
    }

    public IBlockRenderer? Resolve(Block block)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer.CanRender(block)) return renderer;
        }

        return null;
    }
}

/// <summary>
/// Blocks that produce no output: link reference definitions and YAML front
/// matter, which is surfaced separately as document metadata (5.12).
/// </summary>
public sealed class IgnoredBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) =>
        block is LinkReferenceDefinitionGroup or LinkReferenceDefinition
        || block.GetType().Name is "YamlFrontMatterBlock";

    public Control Render(Block block, RenderContext context) => new Panel { IsVisible = false };
}
