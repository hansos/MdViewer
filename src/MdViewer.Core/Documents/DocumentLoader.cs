using Markdig;
using MdViewer.Core.Markdown;
using MdViewer.Core.Outline;

namespace MdViewer.Core.Documents;

/// <summary>
/// Reads and parses a Markdown file (SPECIFICATION.md 5.2, 5.10).
///
/// Everything here runs off the UI thread. Only visual-tree construction, which
/// lives in MdViewer.Rendering, touches it.
/// </summary>
public sealed class DocumentLoader
{
    /// <summary>
    /// Above this size the document opens in reduced mode: parsed, but with
    /// highlighting and diagrams disabled (SPECIFICATION.md 5.10).
    /// </summary>
    public const long ReducedModeThresholdBytes = 5 * 1024 * 1024;

    /// <summary>Above this, the app asks for confirmation before opening.</summary>
    public const long ConfirmationThresholdBytes = 50 * 1024 * 1024;

    private readonly MarkdownPipeline _pipeline;

    public DocumentLoader(MarkdownPipeline? pipeline = null)
    {
        _pipeline = pipeline ?? MarkdownPipelineFactory.Default;
    }

    public async Task<DocumentLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            var bytes = await ReadWithRetryAsync(path, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var text = Decode(bytes, out var encoding);
            cancellationToken.ThrowIfCancellationRequested();

            return DocumentLoadResult.Success(Parse(path, text, encoding));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return DocumentLoadResult.Failure(path, "The file no longer exists.");
        }
        catch (DirectoryNotFoundException)
        {
            return DocumentLoadResult.Failure(path, "The folder no longer exists.");
        }
        catch (UnauthorizedAccessException)
        {
            return DocumentLoadResult.Failure(path, "Access to this file was denied.");
        }
        catch (IOException ex)
        {
            return DocumentLoadResult.Failure(path, $"The file could not be read: {ex.Message}");
        }
    }

    /// <summary>Parses text that is already in memory. Also the entry point for tests.</summary>
    public LoadedDocument Parse(
        string path,
        string text,
        DocumentEncoding encoding = DocumentEncoding.Utf8)
    {
        var ast = Markdig.Markdown.Parse(text, _pipeline);

        return new LoadedDocument(
            path,
            text,
            ast,
            OutlineBuilder.Build(ast),
            encoding,
            LineEndings.Detect(text),
            MarkdownText.CountWords(ast));
    }

    public static string Decode(ReadOnlySpan<byte> bytes, out DocumentEncoding encoding)
    {
        var detection = EncodingDetector.Detect(bytes);
        encoding = detection.Encoding;

        var payload = bytes[detection.BomLength..];
        return EncodingDetector.ToEncoding(detection.Encoding).GetString(payload);
    }

    /// <summary>
    /// Editors commonly write a file as truncate-then-write or delete-then-rename,
    /// so a read that lands mid-save hits a lock or a half-written file. Retrying
    /// briefly turns a spurious error dialog into a non-event
    /// (SPECIFICATION.md 5.10).
    /// </summary>
    private static async Task<byte[]> ReadWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        const int attempts = 5;
        const int delayMs = 100;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) when (attempt < attempts)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
