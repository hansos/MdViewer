namespace MdViewer.Core.Session;

/// <summary>
/// One entry in the recent-documents list (SPECIFICATION.md 5.14).
/// Persisted in session.json; never leaves the machine.
/// </summary>
/// <param name="Path">Absolute, canonical path. The identity of the entry.</param>
/// <param name="Title">Display title — the front-matter title when present, else the file name.</param>
/// <param name="LastOpenedUtc">When the document was last opened.</param>
/// <param name="ScrollOffset">
/// Source offset of the topmost visible element when the document was last closed,
/// so reopening from the recent list restores the reading position.
/// </param>
public sealed record RecentDocument(
    string Path,
    string Title,
    DateTimeOffset LastOpenedUtc,
    int ScrollOffset = 0);
