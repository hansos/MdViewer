namespace MdViewer.Core.Session;

/// <summary>
/// Loads and saves <c>session.json</c> in the platform config directory
/// (SPECIFICATION.md 5.13).
/// </summary>
public sealed class SessionStore
{
    public const string FileName = "session.json";

    private readonly string _path;

    public SessionStore()
        : this(JsonStore.PathFor(FileName))
    {
    }

    /// <summary>Overridable path, so tests never touch the real session file.</summary>
    public SessionStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string Path => _path;

    /// <summary>Never fails: an absent or unreadable file yields an empty session.</summary>
    public SessionState Load() => JsonStore.Load<SessionState>(_path) ?? new SessionState();

    public bool Save(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.SchemaVersion = SessionState.CurrentSchemaVersion;
        return JsonStore.Save(_path, state);
    }
}
