using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MdViewer.Core.Workspace;

namespace MdViewer.App.ViewModels;

/// <summary>
/// One entry in the workspace file tree (SPECIFICATION.md 5.9).
///
/// Directories enumerate on first expand rather than up front, so opening a
/// large workspace costs nothing until the user actually looks inside it.
/// </summary>
public partial class FileTreeItemViewModel : ViewModelBase
{
    private readonly WorkspaceNode _node;
    private readonly WorkspaceScanner _scanner;
    private bool _childrenLoaded;

    [ObservableProperty]
    private bool _isExpanded;

    public FileTreeItemViewModel(WorkspaceNode node, WorkspaceScanner scanner)
    {
        _node = node;
        _scanner = scanner;
        Children = new ObservableCollection<FileTreeItemViewModel>();

        if (node.IsDirectory && node.IsLoaded)
        {
            PopulateChildren();
        }
        else if (node.IsDirectory)
        {
            // A placeholder so the expander chevron appears before the folder
            // has been read. Replaced on first expand.
            Children.Add(Placeholder);
        }
    }

    private static FileTreeItemViewModel Placeholder =>
        new(new WorkspaceNode("…", string.Empty, isDirectory: false), new WorkspaceScanner());

    public string Name => _node.Name;

    public string FullPath => _node.FullPath;

    public bool IsDirectory => _node.IsDirectory;

    public ObservableCollection<FileTreeItemViewModel> Children { get; }

    public string Glyph => IsDirectory ? "📁" : "▤";

    public bool IsMarkdown => !IsDirectory && WorkspaceScanner.IsMarkdown(_node.FullPath);

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || _childrenLoaded || !IsDirectory) return;

        _childrenLoaded = true;
        _scanner.Load(_node);
        PopulateChildren();
    }

    private void PopulateChildren()
    {
        Children.Clear();
        foreach (var child in _node.Children)
        {
            Children.Add(new FileTreeItemViewModel(child, _scanner));
        }

        _childrenLoaded = true;
    }
}
