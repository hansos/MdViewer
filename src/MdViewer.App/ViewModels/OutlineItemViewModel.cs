using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MdViewer.Core.Outline;

namespace MdViewer.App.ViewModels;

/// <summary>
/// One heading in the outline panel (SPECIFICATION.md 5.8).
/// Collapse state is per tab and is persisted in the session.
/// </summary>
public partial class OutlineItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isCurrent;

    public OutlineItemViewModel(OutlineNode node)
    {
        Title = node.Title;
        Level = node.Level;
        Anchor = node.Anchor;
        SourceOffset = node.SourceOffset;
        Children = new ObservableCollection<OutlineItemViewModel>(
            node.Children.Select(c => new OutlineItemViewModel(c)));
    }

    public string Title { get; }

    public int Level { get; }

    public string Anchor { get; }

    public int SourceOffset { get; }

    public ObservableCollection<OutlineItemViewModel> Children { get; }

    /// <summary>H1 sits slightly heavier than deeper levels in the panel.</summary>
    public FontWeight LevelWeight => Level <= 1 ? FontWeight.SemiBold : FontWeight.Normal;

    public bool HasChildren => Children.Count > 0;
}
