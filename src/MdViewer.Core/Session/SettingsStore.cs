namespace MdViewer.Core.Session;

/// <summary>
/// Loads and saves <c>settings.json</c> in the platform config directory
/// (SPECIFICATION.md 5.13).
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    private readonly string _path;

    public SettingsStore()
        : this(JsonStore.PathFor(FileName))
    {
    }

    /// <summary>Overridable path, so tests never touch the real config file.</summary>
    public SettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string Path => _path;

    /// <summary>Never fails: an absent or unreadable file yields defaults.</summary>
    public AppSettings Load() => JsonStore.Load<AppSettings>(_path) ?? new AppSettings();

    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        return JsonStore.Save(_path, settings);
    }
}
