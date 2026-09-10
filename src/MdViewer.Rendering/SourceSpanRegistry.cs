using Avalonia.Controls;

namespace MdViewer.Rendering;

/// <summary>
/// Records which source range each rendered control came from
/// (SPECIFICATION.md 5.2).
///
/// This registry is what makes offset-based scroll restoration, outline
/// scroll-to, find highlighting and the v2 editor's scroll sync possible. Every
/// renderer must register what it creates; a renderer that forgets silently
/// removes its blocks from all four features.
/// </summary>
public sealed class SourceSpanRegistry : ISourceSpanRegistrar
{
    private readonly Dictionary<Control, (int Start, int Length)> _byControl = new();
    private readonly List<(int Start, int Length, Control Control)> _ordered = new();
    private bool _sorted = true;

    public void Register(Control control, int sourceStart, int sourceLength)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (sourceStart < 0) sourceStart = 0;
        if (sourceLength < 0) sourceLength = 0;

        _byControl[control] = (sourceStart, sourceLength);
        _ordered.Add((sourceStart, sourceLength, control));
        _sorted = false;
    }

    public bool TryGetSpan(Control control, out int sourceStart, out int sourceLength)
    {
        if (_byControl.TryGetValue(control, out var span))
        {
            sourceStart = span.Start;
            sourceLength = span.Length;
            return true;
        }

        sourceStart = 0;
        sourceLength = 0;
        return false;
    }

    /// <summary>
    /// The innermost control whose span contains the offset, or the nearest
    /// control that starts before it. Returning the nearest rather than null is
    /// deliberate: restoring a scroll position into a document that has changed
    /// should land close by, not at the top.
    /// </summary>
    public Control? FindControlContaining(int sourceOffset)
    {
        EnsureSorted();
        if (_ordered.Count == 0) return null;

        Control? best = null;
        var bestLength = int.MaxValue;

        foreach (var (start, length, control) in _ordered)
        {
            if (start > sourceOffset) break;

            if (start + length >= sourceOffset && length < bestLength)
            {
                best = control;
                bestLength = length;
            }

            best ??= control;
        }

        return best ?? _ordered[0].Control;
    }

    /// <summary>The source offset of the first control at or after the offset.</summary>
    public int GetSourceOffset(Control control) =>
        TryGetSpan(control, out var start, out _) ? start : 0;

    /// <summary>
    /// Every registered control in source order. Used to answer "which block is
    /// at the top of the viewport", which is how the reading position survives
    /// a switch between preview and source (SPECIFICATION.md 5.15).
    /// </summary>
    public IReadOnlyList<RegisteredSpan> Ordered
    {
        get
        {
            EnsureSorted();
            var result = new RegisteredSpan[_ordered.Count];
            for (var i = 0; i < _ordered.Count; i++)
            {
                var (start, length, control) = _ordered[i];
                result[i] = new RegisteredSpan(start, length, control);
            }

            return result;
        }
    }

    public readonly record struct RegisteredSpan(int Start, int Length, Control Control);

    public void Clear()
    {
        _byControl.Clear();
        _ordered.Clear();
        _sorted = true;
    }

    private void EnsureSorted()
    {
        if (_sorted) return;

        _ordered.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        _sorted = true;
    }
}
