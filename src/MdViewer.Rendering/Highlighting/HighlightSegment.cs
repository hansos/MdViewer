namespace MdViewer.Rendering.Highlighting;

/// <summary>
/// One run of code text and the theme token it should be painted with
/// (SPECIFICATION.md 5.3).
///
/// Segments are produced off the UI thread and carry a token KEY rather than a
/// colour, so the result of a tokenise survives a theme switch: the runs rebind
/// and repaint, and nothing is re-parsed.
/// </summary>
/// <param name="Text">The literal text of this run, verbatim from the source.</param>
/// <param name="TokenKey">
/// A key from <see cref="Themed.Keys"/>, or <c>null</c> for text that carries no
/// highlight and inherits the block's foreground.
/// </param>
public readonly record struct HighlightSegment(string Text, string? TokenKey);
