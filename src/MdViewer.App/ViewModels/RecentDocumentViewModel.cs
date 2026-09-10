using MdViewer.Core.Session;

namespace MdViewer.App.ViewModels;

/// <summary>
/// One card in the start view's recent list (SPECIFICATION.md 5.14).
/// </summary>
public sealed class RecentDocumentViewModel : ViewModelBase
{
    public RecentDocumentViewModel(RecentDocument document, DateTimeOffset now, bool exists = true)
    {
        Document = document;
        Exists = exists;
        Age = FormatAge(now - document.LastOpenedUtc);
    }

    public RecentDocument Document { get; }

    public string Title => Document.Title;

    public string FullPath => Document.Path;

    /// <summary>The containing directory, shown under the title.</summary>
    public string Directory
    {
        get
        {
            var path = Document.Path;
            var slash = path.LastIndexOfAny(new[] { '\\', '/' });
            return slash <= 0 ? path : path[..slash];
        }
    }

    /// <summary>
    /// False when the file is gone. Checked at display time, never at startup —
    /// the entry is shown as unavailable rather than silently dropped.
    /// </summary>
    public bool Exists { get; }

    public string Age { get; }

    public string StatusText => Exists ? Age : "File not found";

    private static string FormatAge(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) return "just now";
        if (elapsed.TotalSeconds < 90) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours} h ago";
        if (elapsed.TotalDays < 2) return "yesterday";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays} days ago";
        return $"{(int)(elapsed.TotalDays / 7)} weeks ago";
    }
}
