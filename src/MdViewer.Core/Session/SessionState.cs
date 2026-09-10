using System.Text.Json;
using System.Text.Json.Serialization;
using MdViewer.Core.Documents;

namespace MdViewer.Core.Session;

/// <summary>
/// One restored tab: where the document is, how it was being read, and how it
/// was being displayed (SPECIFICATION.md 5.13).
/// </summary>
public sealed class SessionTab
{
    public string Path { get; set; } = string.Empty;

    /// <summary>Reading position as a source offset, not a pixel offset.</summary>
    public int ScrollOffset { get; set; }

    public DocumentViewMode ViewMode { get; set; } = DocumentViewMode.Preview;

    public double Zoom { get; set; } = 1.0;
}

/// <summary>
/// The contents of <c>session.json</c> (SPECIFICATION.md 5.13): what was open
/// and where the window was, restored on the next launch.
///
/// Like <see cref="AppSettings"/>, unknown keys are preserved on write.
/// </summary>
public sealed class SessionState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<SessionTab> Tabs { get; set; } = new();

    /// <summary>Index into <see cref="Tabs"/>, or -1 when nothing was active.</summary>
    public int ActiveTabIndex { get; set; } = -1;

    public bool IsSidebarVisible { get; set; } = true;

    public double SidebarWidth { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public double? WindowX { get; set; }

    public double? WindowY { get; set; }

    public bool IsMaximized { get; set; }

    /// <summary>Recent documents (SPECIFICATION.md 5.14), most recent first.</summary>
    public List<SessionRecentDocument> Recent { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// A serializable projection of <see cref="RecentDocument"/>. The domain type
/// stays a record with the shape the app wants; this one has the shape JSON
/// wants, and the two are mapped explicitly rather than coupled.
/// </summary>
public sealed class SessionRecentDocument
{
    public string Path { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset LastOpenedUtc { get; set; }

    public int ScrollOffset { get; set; }
}
