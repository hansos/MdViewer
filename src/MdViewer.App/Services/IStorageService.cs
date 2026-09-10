namespace MdViewer.App.Services;

/// <summary>
/// File and folder pickers, behind an interface so the view models stay free
/// of Avalonia and remain testable without a UI thread (SPECIFICATION.md 2.3).
/// </summary>
public interface IStorageService
{
    Task<IReadOnlyList<string>> PickMarkdownFilesAsync();

    Task<string?> PickFolderAsync();
}
