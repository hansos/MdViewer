using System.Text.Json;

namespace MdViewer.Core.Session;

/// <summary>
/// Reads and writes the app's JSON config files (SPECIFICATION.md 5.13).
///
/// Two rules shape this type. Reads are forgiving: a missing, truncated or
/// hand-edited file yields defaults rather than an exception, because startup
/// must never be blocked by a bad config file. Writes are atomic — a temporary
/// file is written and then renamed over the target — so a crash mid-write
/// cannot leave a half-written session behind.
/// </summary>
public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// The platform config directory: <c>%APPDATA%\MdViewer</c> on Windows,
    /// <c>$XDG_CONFIG_HOME/mdviewer</c> (or <c>~/.config/mdviewer</c>) elsewhere.
    /// </summary>
    public static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "MdViewer");
            }

            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(xdg))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                xdg = Path.Combine(home, ".config");
            }

            return Path.Combine(xdg, "mdviewer");
        }
    }

    public static string PathFor(string fileName) => Path.Combine(ConfigDirectory, fileName);

    /// <summary>Loads a file, or returns null if it is absent or unreadable.</summary>
    public static T? Load<T>(string path)
        where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (Exception)
        {
            // A corrupt or unreadable config is not worth a crash, and not
            // worth a dialog either: defaults are a perfectly good answer.
            return null;
        }
    }

    /// <summary>
    /// Writes a file atomically. Returns false rather than throwing, since a
    /// failed config write must not take the app down with it.
    /// </summary>
    public static bool Save<T>(string path, T value)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));

            // Move overwrites in one step, so readers see either the old file
            // or the new one and never a partial write.
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
