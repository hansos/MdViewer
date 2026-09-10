namespace MdViewer.Core.Workspace;

/// <summary>
/// An entry in the workspace file tree (SPECIFICATION.md 5.9).
/// Directory contents are read on expand, not up front — the
/// <see cref="IsLoaded"/> flag carries that state.
/// </summary>
public sealed class WorkspaceNode
{
    public WorkspaceNode(string name, string fullPath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Children = new List<WorkspaceNode>();
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    /// <summary>False until the directory has been enumerated.</summary>
    public bool IsLoaded { get; set; }

    public List<WorkspaceNode> Children { get; }

    public WorkspaceNode AddChild(WorkspaceNode child)
    {
        Children.Add(child);
        return this;
    }
}
