namespace MdViewer.Rendering;

/// <summary>One find match in source-offset space.</summary>
public readonly record struct FindMatchOccurrence(int Start, int Length);
