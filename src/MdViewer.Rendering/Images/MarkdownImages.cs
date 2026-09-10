using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Markdig.Syntax.Inlines;
using MdViewer.Core.Markdown;

namespace MdViewer.Rendering.Images;

/// <summary>
/// Loads and presents Markdown images (SPECIFICATION.md 5.6).
///
/// Block images and inline images share this so both obey the same rules:
/// local relative paths resolve against the document directory, remote images
/// are never fetched without per-document consent, and anything that cannot be
/// loaded becomes a placeholder rather than an exception.
/// </summary>
internal static class MarkdownImages
{
    /// <summary>
    /// Inline images sit in a line of text, so they are capped to something
    /// that does not turn a paragraph into a wall of picture. Block images get
    /// the full content column.
    /// </summary>
    private const double InlineMaxHeight = 180;

    private static readonly HttpClient RemoteClient = CreateRemoteClient();

    private static HttpClient CreateRemoteClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        // Some image hosts reject requests that arrive without a user agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MdViewer");
        return client;
    }

    public static string AltText(LinkInline image)
    {
        var alt = MarkdownText.ToPlainText(image);
        return string.IsNullOrWhiteSpace(alt) ? "image" : alt.Trim();
    }

    /// <summary>A full-width image, or a placeholder card explaining why not.</summary>
    public static Control CreateBlock(LinkInline image, RenderContext context)
    {
        var altText = AltText(image);
        var resolution = Resolve(image, context);

        return resolution.Kind switch
        {
            ImageResultKind.Loaded => BuildImage(resolution.Bitmap!, altText, inline: false),
            ImageResultKind.RemoteConsentRequired => BuildRemotePlaceholder(altText, context, inline: false),
            _ => BuildBrokenPlaceholder(altText, resolution.Message, inline: false)
        };
    }

    /// <summary>The same image, sized for a line of running text.</summary>
    public static Control CreateInline(LinkInline image, RenderContext context)
    {
        var altText = AltText(image);
        var resolution = Resolve(image, context);

        return resolution.Kind switch
        {
            ImageResultKind.Loaded => BuildImage(resolution.Bitmap!, altText, inline: true),
            ImageResultKind.RemoteConsentRequired => BuildRemotePlaceholder(altText, context, inline: true),
            _ => BuildBrokenPlaceholder(altText, resolution.Message, inline: true)
        };
    }

    private static ImageResult Resolve(LinkInline image, RenderContext context)
    {
        var rawTarget = image.Url ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return ImageResult.Broken("Image target is missing.");
        }

        var kind = Classify(rawTarget, context.BaseDirectory, out var localPath, out var remoteUri);

        return kind switch
        {
            ImageSourceKind.Local => LoadLocal(localPath!),
            ImageSourceKind.Remote => context.AllowRemoteImages
                ? LoadRemote(remoteUri!)
                : ImageResult.RemoteConsentRequired(),
            ImageSourceKind.RelativeWithoutBase =>
                ImageResult.Broken("Relative image paths need a saved document."),
            _ => ImageResult.Broken("Unsupported image URL scheme.")
        };
    }

    private static ImageResult LoadLocal(string path)
    {
        if (!File.Exists(path))
        {
            return ImageResult.Broken("Image file not found.");
        }

        try
        {
            return ImageResult.Loaded(new Bitmap(path));
        }
        catch
        {
            return ImageResult.Broken("Image file could not be read.");
        }
    }

    private static ImageResult LoadRemote(Uri uri)
    {
        try
        {
            using var response = RemoteClient
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode)
            {
                return ImageResult.Broken($"Remote image failed: {(int)response.StatusCode}.");
            }

            // The decoder needs to seek, which a live network stream cannot do,
            // so the bytes are buffered before they reach the bitmap.
            using var network = response.Content.ReadAsStream();
            using var buffer = new MemoryStream();
            network.CopyTo(buffer);
            buffer.Position = 0;

            return ImageResult.Loaded(new Bitmap(buffer));
        }
        catch
        {
            return ImageResult.Broken("Remote image could not be loaded.");
        }
    }

    /// <summary>
    /// Uniform scaling with a natural-size cap: an image shrinks to fit the
    /// column but is never blown up past its own pixels (SPECIFICATION.md 5.6).
    /// </summary>
    private static Control BuildImage(Bitmap bitmap, string altText, bool inline)
    {
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            VerticalAlignment = VerticalAlignment.Top
        };

        if (bitmap.Size.Width > 0)
        {
            image.MaxWidth = bitmap.Size.Width;
        }

        if (bitmap.Size.Height > 0)
        {
            image.MaxHeight = inline
                ? Math.Min(bitmap.Size.Height, InlineMaxHeight)
                : bitmap.Size.Height;
        }

        if (inline && bitmap.Size is { Width: > 0, Height: > 0 })
        {
            // An inline image lives inside a text line, and a text line only
            // reserves room for what it is told about up front. Without a fixed
            // size the line keeps its normal height and clips the picture, so
            // the scaled size is baked in here.
            var scale = Math.Min(1.0, InlineMaxHeight / bitmap.Size.Height);
            image.Width = bitmap.Size.Width * scale;
            image.Height = bitmap.Size.Height * scale;
        }

        image.Classes.Add("md-image");
        if (inline) image.Classes.Add("inline");

        ToolTip.SetTip(image, altText);

        var frame = new Border { Child = image };
        frame.Classes.Add("md-image-block");
        if (inline) frame.Classes.Add("inline");

        return frame;
    }

    private static Control BuildRemotePlaceholder(string altText, RenderContext context, bool inline)
    {
        var title = new TextBlock { Text = $"[{altText}]", TextWrapping = TextWrapping.Wrap };
        title.Classes.Add("md-callout-title");

        var detail = new TextBlock
        {
            Text = "Remote image blocked until you allow images for this document.",
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("md-caption");

        var button = new Button
        {
            Content = "Load images",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = context.OnLoadRemoteImagesRequested is not null
        };

        button.Classes.Add("md-image-load");
        button.Click += (_, _) => context.OnLoadRemoteImagesRequested?.Invoke();

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(title);
        stack.Children.Add(detail);
        stack.Children.Add(button);

        return BuildPlaceholderFrame(stack, "md-image-remote", inline);
    }

    private static Control BuildBrokenPlaceholder(string altText, string detailText, bool inline)
    {
        var title = new TextBlock { Text = $"[{altText}]", TextWrapping = TextWrapping.Wrap };
        title.Classes.Add("md-callout-title");

        var detail = new TextBlock { Text = detailText, TextWrapping = TextWrapping.Wrap };
        detail.Classes.Add("md-caption");

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(title);
        stack.Children.Add(detail);

        return BuildPlaceholderFrame(stack, "md-image-broken", inline);
    }

    private static Control BuildPlaceholderFrame(Control content, string variantClass, bool inline)
    {
        var frame = new Border { Child = content };
        frame.Classes.Add("md-image-placeholder");
        frame.Classes.Add(variantClass);
        if (inline) frame.Classes.Add("inline");

        return frame;
    }

    private static ImageSourceKind Classify(
        string rawTarget,
        string baseDirectory,
        out string? localPath,
        out Uri? remoteUri)
    {
        localPath = null;
        remoteUri = null;

        var target = rawTarget.Trim();

        // A target may carry a fragment or query that is meaningless on disk.
        var decoded = Uri.UnescapeDataString(target);

        if (Uri.TryCreate(decoded, UriKind.Absolute, out var absolute))
        {
            if (absolute.IsFile)
            {
                localPath = absolute.LocalPath;
                return ImageSourceKind.Local;
            }

            if (absolute.Scheme is "http" or "https")
            {
                remoteUri = absolute;
                return ImageSourceKind.Remote;
            }

            return ImageSourceKind.Unsupported;
        }

        var cleaned = StripFragmentAndQuery(decoded);
        if (cleaned.Length == 0)
        {
            return ImageSourceKind.Unsupported;
        }

        if (Path.IsPathRooted(cleaned))
        {
            localPath = Path.GetFullPath(cleaned);
            return ImageSourceKind.Local;
        }

        if (string.IsNullOrEmpty(baseDirectory))
        {
            return ImageSourceKind.RelativeWithoutBase;
        }

        localPath = Path.GetFullPath(Path.Combine(baseDirectory, cleaned));
        return ImageSourceKind.Local;
    }

    private static string StripFragmentAndQuery(string value)
    {
        var end = value.Length;

        var hash = value.IndexOf('#');
        if (hash >= 0) end = Math.Min(end, hash);

        var query = value.IndexOf('?');
        if (query >= 0) end = Math.Min(end, query);

        return value[..end].Trim();
    }

    private enum ImageSourceKind
    {
        Local,
        Remote,
        RelativeWithoutBase,
        Unsupported
    }

    private enum ImageResultKind
    {
        Loaded,
        RemoteConsentRequired,
        Broken
    }

    private readonly struct ImageResult
    {
        private ImageResult(ImageResultKind kind, Bitmap? bitmap, string message)
        {
            Kind = kind;
            Bitmap = bitmap;
            Message = message;
        }

        public ImageResultKind Kind { get; }

        public Bitmap? Bitmap { get; }

        public string Message { get; }

        public static ImageResult Loaded(Bitmap bitmap) =>
            new(ImageResultKind.Loaded, bitmap, string.Empty);

        public static ImageResult RemoteConsentRequired() =>
            new(ImageResultKind.RemoteConsentRequired, null, string.Empty);

        public static ImageResult Broken(string message) =>
            new(ImageResultKind.Broken, null, message);
    }
}
