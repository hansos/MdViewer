namespace MdViewer.Core.Workspace;

/// <summary>
/// Enumerates a workspace folder for the file tree (SPECIFICATION.md 5.9).
///
/// Directories are read on expand, not up front: a workspace root can contain
/// a hundred thousand files, and walking it eagerly would stall the window on
/// open for a tree the user may never expand.
/// </summary>
public sealed class WorkspaceScanner
{
    private static readonly string[] MarkdownExtensions =
        { ".md", ".markdown", ".mdown", ".mkd", ".mdx" };

    /// <summary>
    /// Always hidden. Overridable in settings, but these four are noise in every
    /// workspace anyone has ever opened.
    /// </summary>
    private static readonly HashSet<string> AlwaysHidden =
        new(StringComparer.OrdinalIgnoreCase) { ".git", "node_modules", "bin", "obj" };

    public bool ShowAllFiles { get; init; }

    public bool ShowHiddenEntries { get; init; }

    public static bool IsMarkdown(string path) =>
        MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the root node without enumerating it.</summary>
    public WorkspaceNode CreateRoot(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        var full = Path.GetFullPath(directory);
        var name = new DirectoryInfo(full).Name;

        return new WorkspaceNode(string.IsNullOrEmpty(name) ? full : name, full, isDirectory: true);
    }

    /// <summary>
    /// Fills a directory node's children. Safe to call more than once; a second
    /// call re-reads, which is what the file watcher wants.
    /// </summary>
    public void Load(WorkspaceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsDirectory) return;

        node.Children.Clear();

        try
        {
            var entries = new DirectoryInfo(node.FullPath);

            foreach (var directory in entries.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (ShouldHide(directory.Name, directory.Attributes)) continue;
                node.AddChild(new WorkspaceNode(directory.Name, directory.FullName, isDirectory: true));
            }

            foreach (var file in entries.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (ShouldHide(file.Name, file.Attributes)) continue;
                if (!ShowAllFiles && !IsMarkdown(file.Name)) continue;

                node.AddChild(new WorkspaceNode(file.Name, file.FullName, isDirectory: false));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            // An unreadable folder is shown empty rather than failing the tree.
            // On Flatpak in particular this is a normal, expected outcome.
        }

        node.IsLoaded = true;
    }

    /// <summary>
    /// Walks the tree for the quick-open index, breadth-first with a cap
    /// (SPECIFICATION.md 5.9).
    /// </summary>
    public IReadOnlyList<WorkspaceNode> EnumerateFiles(string root, int limit = 50_000)
    {
        var results = new List<WorkspaceNode>();
        var queue = new Queue<string>();
        queue.Enqueue(Path.GetFullPath(root));

        while (queue.Count > 0 && results.Count < limit)
        {
            var current = queue.Dequeue();

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    if (results.Count >= limit) break;
                    if (!ShowAllFiles && !IsMarkdown(file)) continue;

                    results.Add(new WorkspaceNode(Path.GetFileName(file), file, isDirectory: false));
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    var name = Path.GetFileName(directory);
                    if (ShouldHide(name, FileAttributes.Directory)) continue;
                    queue.Enqueue(directory);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                // Skip and carry on.
            }
        }

        return results;
    }

    private bool ShouldHide(string name, FileAttributes attributes)
    {
        if (AlwaysHidden.Contains(name)) return true;
        if (ShowHiddenEntries) return false;

        if (name.StartsWith('.')) return true;
        return attributes.HasFlag(FileAttributes.Hidden);
    }
}
