using Avalonia.Controls;

namespace MdViewer.App.Views;

/// <summary>
/// A hand-authored stand-in for rendered Markdown.
///
/// Nothing here is produced by a parser: every element is written by hand so
/// that typography, spacing, colour tokens and block chrome can be judged
/// before MdViewer.Rendering exists. When the real block renderers land in
/// M2/M3 they must reproduce this output from a parsed document, and this
/// file becomes the visual reference they are checked against.
/// </summary>
public partial class SampleDocumentView : UserControl
{
    public SampleDocumentView()
    {
        InitializeComponent();
    }
}
