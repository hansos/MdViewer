using Avalonia.Controls;

namespace MdViewer.App.Views;

/// <summary>
/// The about page. It fills the document pane rather than opening a modal
/// dialog, matching how the settings page behaves.
/// </summary>
public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }
}
