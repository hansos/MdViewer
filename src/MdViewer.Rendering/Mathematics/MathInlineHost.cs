using Avalonia;
using Avalonia.Controls;

namespace MdViewer.Rendering.Mathematics;

/// <summary>
/// Sits an inline formula on the text line (SPECIFICATION.md 5.1 mathematics).
///
/// An embedded control is placed with its bottom edge on the baseline, so a
/// fraction or a root — which is taller than the letters around it — rides high
/// above the line. This host reserves the space needed below the formula so the
/// formula's own centre ends up on the optical centre of the text instead.
///
/// The offset is derived during measure, from the size the formula asks for.
/// Nothing is written back into a layout property afterwards, which is what
/// keeps the line steady: an earlier version corrected the position from
/// <c>LayoutUpdated</c> and each correction triggered another layout pass.
/// </summary>
internal sealed class MathInlineHost : Decorator
{
    /// <summary>
    /// Where the optical centre of a line of text sits above the baseline, as a
    /// fraction of the font size (roughly half the x-height).
    /// </summary>
    private const double TextCentreAboveBaseline = 0.32;

    private readonly double _fontSize;

    public MathInlineHost(Control formula, double fontSize)
    {
        _fontSize = fontSize;
        Child = formula;

        // The formula deliberately draws past the bottom of the reported box,
        // which is only visible if the host does not clip to its own bounds.
        ClipToBounds = false;
    }

    /// <summary>
    /// How far the formula has to drop below the text baseline so that its own
    /// centre lands on the centre of the surrounding text.
    /// </summary>
    private double Overhang
    {
        get
        {
            var height = Child?.DesiredSize.Height ?? 0;

            // The host's bottom edge is what gets placed on the baseline, so a
            // formula is centred by letting it hang below that edge by half its
            // height, less the height the text already carries above the line.
            var overhang = (height / 2) - (_fontSize * TextCentreAboveBaseline);

            // A short formula already sits on the line; only a tall one needs to
            // reach below it, and it never drops further than half its height.
            return overhang > 0 ? overhang : 0;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }

        child.Measure(availableSize);

        var size = child.DesiredSize;

        // Reporting less height than the formula occupies is what lowers it:
        // the text line puts this shorter box on the baseline and the formula
        // keeps drawing past the bottom of it.
        return new Size(size.Width, size.Height - Overhang);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }

        // Arranged at its full height, so the part below the baseline is drawn
        // rather than squeezed into the reported box.
        child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));

        return finalSize;
    }
}
