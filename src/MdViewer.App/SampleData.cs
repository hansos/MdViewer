using MdViewer.Core.Outline;
using MdViewer.Core.Session;
using MdViewer.Core.Workspace;

namespace MdViewer.App;

/// <summary>
/// Hand-built stand-ins for what the parser, outline extractor and workspace
/// scanner will produce. This exists so the GUI can be evaluated before any
/// internal function is implemented, and is deleted in M2/M6 when the real
/// producers land.
/// </summary>
internal static class SampleData
{
    public static IReadOnlyList<OutlineNode> Outline()
    {
        var purpose = new OutlineNode("1  Purpose and Goals", 1, "purpose-and-goals", 0);
        purpose.AddChild(new OutlineNode("1.1  Design goals", 2, "design-goals", 120));
        purpose.AddChild(new OutlineNode("1.2  Non-goals for v1", 2, "non-goals", 480));

        var tech = new OutlineNode("2  Technology Decisions", 1, "technology-decisions", 900);
        var render = new OutlineNode("2.1  Rendering approach", 2, "rendering-approach", 960);
        render.AddChild(new OutlineNode("Native vs WebView", 3, "native-vs-webview", 1010));
        render.AddChild(new OutlineNode("Consequences", 3, "consequences", 1740));
        tech.AddChild(render);
        tech.AddChild(new OutlineNode("2.2  Dependencies", 2, "dependencies", 2100));
        tech.AddChild(new OutlineNode("2.3  Architectural pattern", 2, "architectural-pattern", 2680));

        var features = new OutlineNode("3  Feature Specifications", 1, "feature-specifications", 3200);
        features.AddChild(new OutlineNode("3.1  Markdown dialect", 2, "markdown-dialect", 3260));
        features.AddChild(new OutlineNode("3.2  Rendering pipeline", 2, "rendering-pipeline", 3900));
        features.AddChild(new OutlineNode("3.3  Code blocks", 2, "code-blocks", 4600));
        features.AddChild(new OutlineNode("3.4  Tables", 2, "tables", 5300));

        var nfr = new OutlineNode("4  Non-Functional Requirements", 1, "non-functional", 6000);
        nfr.AddChild(new OutlineNode("4.1  Performance targets", 2, "performance-targets", 6080));
        nfr.AddChild(new OutlineNode("4.2  Virtualization", 2, "virtualization", 6700));
        nfr.AddChild(new OutlineNode("4.3  Accessibility", 2, "accessibility", 7200));

        return new[] { purpose, tech, features, nfr };
    }

    /// <summary>
    /// Stand-in for the persisted recent list (SPECIFICATION.md 5.14).
    /// One entry is deliberately marked missing so the unavailable state
    /// can be evaluated — that case is the whole reason existence is checked
    /// at display time rather than at startup.
    /// </summary>
    public static IReadOnlyList<(RecentDocument Document, bool Exists)> Recent(DateTimeOffset now)
    {
        return new[]
        {
            (new RecentDocument(
                @"C:\Source2\MdViewer\docs\SPECIFICATION.md",
                "MdViewer — Specification",
                now.AddMinutes(-3),
                4_820), true),

            (new RecentDocument(
                @"C:\Source2\MdViewer\README.md",
                "README.md",
                now.AddMinutes(-48),
                0), true),

            (new RecentDocument(
                @"C:\Source2\MdViewer\samples\tables-torture.md",
                "tables-torture.md",
                now.AddHours(-5),
                1_140), true),

            (new RecentDocument(
                @"C:\Notes\meeting-2026-09-02.md",
                "Sprint planning",
                now.AddDays(-1).AddHours(-2),
                260), false),

            (new RecentDocument(
                @"C:\Source2\MdViewer\CHANGELOG.md",
                "CHANGELOG.md",
                now.AddDays(-4),
                0), true)
        };
    }

    /// <summary>
    /// Flattened Markdown files for quick-open. The real index is built on
    /// workspace open and maintained by the watcher (SPECIFICATION.md 5.9).
    /// </summary>
    public static IReadOnlyList<(string Title, string FullPath)> WorkspaceFiles()
    {
        var results = new List<(string, string)>();

        void Walk(WorkspaceNode node)
        {
            if (!node.IsDirectory)
            {
                results.Add((node.Name, node.FullPath));
            }

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        foreach (var root in Workspace())
        {
            Walk(root);
        }

        return results;
    }

    public static IReadOnlyList<WorkspaceNode> Workspace()
    {
        var root = new WorkspaceNode("MdViewer", @"C:\Source2\MdViewer", true) { IsLoaded = true };

        var docs = new WorkspaceNode("docs", @"C:\Source2\MdViewer\docs", true) { IsLoaded = true };
        docs.AddChild(new WorkspaceNode("SPECIFICATION.md", @"C:\Source2\MdViewer\docs\SPECIFICATION.md", false));
        docs.AddChild(new WorkspaceNode("ARCHITECTURE.md", @"C:\Source2\MdViewer\docs\ARCHITECTURE.md", false));
        docs.AddChild(new WorkspaceNode("CONTRIBUTING.md", @"C:\Source2\MdViewer\docs\CONTRIBUTING.md", false));

        var samples = new WorkspaceNode("samples", @"C:\Source2\MdViewer\samples", true) { IsLoaded = true };
        samples.AddChild(new WorkspaceNode("commonmark.md", @"C:\Source2\MdViewer\samples\commonmark.md", false));
        samples.AddChild(new WorkspaceNode("tables-torture.md", @"C:\Source2\MdViewer\samples\tables-torture.md", false));
        samples.AddChild(new WorkspaceNode("nested-lists.md", @"C:\Source2\MdViewer\samples\nested-lists.md", false));
        samples.AddChild(new WorkspaceNode("cjk-rtl-emoji.md", @"C:\Source2\MdViewer\samples\cjk-rtl-emoji.md", false));

        var src = new WorkspaceNode("src", @"C:\Source2\MdViewer\src", true);
        src.AddChild(new WorkspaceNode("MdViewer.Core", @"C:\Source2\MdViewer\src\MdViewer.Core", true));
        src.AddChild(new WorkspaceNode("MdViewer.Rendering", @"C:\Source2\MdViewer\src\MdViewer.Rendering", true));
        src.AddChild(new WorkspaceNode("MdViewer.App", @"C:\Source2\MdViewer\src\MdViewer.App", true));

        root.AddChild(docs);
        root.AddChild(samples);
        root.AddChild(src);
        root.AddChild(new WorkspaceNode("README.md", @"C:\Source2\MdViewer\README.md", false));
        root.AddChild(new WorkspaceNode("CHANGELOG.md", @"C:\Source2\MdViewer\CHANGELOG.md", false));

        return new[] { root };
    }
}
