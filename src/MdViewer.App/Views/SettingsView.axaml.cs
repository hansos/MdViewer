using Avalonia.Controls;

namespace MdViewer.App.Views;

/// <summary>
/// The settings page (SPECIFICATION.md 5.13). It fills the document pane
/// rather than opening a modal dialog, so it can be scrolled and left open
/// while the shell keeps working.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
