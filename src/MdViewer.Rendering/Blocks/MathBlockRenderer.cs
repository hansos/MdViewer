using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using MdViewer.Rendering.Mathematics;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Display math fenced with <c>$$</c> delimiters (SPECIFICATION.md 5.1).
///
/// The formula is typeset by CSharpMath and centred on its own line, which is
/// how a display equation is set in print.
/// </summary>
public sealed class MathBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is MathBlock;

    public Control Render(Block block, RenderContext context)
    {
        var math = (MathBlock)block;

        var formula = MathTypesetter.CreateBlock(ExtractText(math));
        formula.HorizontalAlignment = HorizontalAlignment.Center;

        var frame = new Border { Child = formula };
        frame.Classes.Add("md-math");

        context.SpanRegistrar.Register(frame, math.Span.Start, math.Span.Length);
        return frame;
    }

    private static string ExtractText(MathBlock math)
    {
        var lines = math.Lines.Lines;
        if (lines is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < math.Lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(lines[i].Slice.ToString());
        }

        return builder.ToString();
    }
}
