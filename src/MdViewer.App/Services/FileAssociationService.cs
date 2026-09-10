using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MdViewer.App.Services;

/// <summary>
/// Registers MdViewer as a handler for Markdown files. Windows does not let an
/// application take over a file type silently, so this writes the per-user
/// ProgID and "Open with" registration and then opens the Default apps page
/// where the user makes the final choice.
/// </summary>
public static class FileAssociationService
{
    private const string ProgId = "Kveldstid.MdViewer.Markdown";

    private static readonly string[] Extensions = [".md", ".markdown", ".mdown", ".mkd"];

    /// <summary>Whether the current platform supports the request at all.</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Registers the ProgID and asks Windows to show the default apps UI.
    /// Returns a status message describing the outcome.
    /// </summary>
    public static string Register()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "Setting the default Markdown app is only supported on Windows.";
        }

        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable))
            {
                return "Could not determine the MdViewer executable path.";
            }

            WriteRegistration(executable);
            NotifyShell();
            OpenDefaultAppsSettings();

            return "MdViewer is registered for Markdown files — pick it in the Windows Default apps page.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return $"Could not register MdViewer for Markdown files: {ex.Message}";
        }
    }

    [SupportedOSPlatform("windows")]
    private static void WriteRegistration(string executable)
    {
        using (var progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progId.SetValue(null, "Markdown Document");
            using var icon = progId.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{executable}\",0");
            using var command = progId.CreateSubKey(@"shell\open\command");
            command.SetValue(null, $"\"{executable}\" \"%1\"");
        }

        foreach (var extension in Extensions)
        {
            using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            using var openWith = extensionKey.CreateSubKey("OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        using var applications = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\Applications\{Path.GetFileName(executable)}\shell\open\command");
        applications.SetValue(null, $"\"{executable}\" \"%1\"");
    }

    [SupportedOSPlatform("windows")]
    private static void NotifyShell() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    private static void OpenDefaultAppsSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true })?.Dispose();

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
