using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MdViewer.Rendering.Diagrams;

public sealed class DiagramView : Decorator
{
    public static readonly StyledProperty<string?> SvgProperty =
        AvaloniaProperty.Register<DiagramView, string?>(nameof(Svg));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<DiagramView, string?>(nameof(ErrorText));

    public static readonly StyledProperty<IBrush?> ForegroundTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(ForegroundToken));

    public static readonly StyledProperty<IBrush?> BackgroundTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(BackgroundToken));

    public static readonly StyledProperty<IBrush?> SurfaceTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(SurfaceToken));

    public static readonly StyledProperty<IBrush?> BorderTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(BorderToken));

    public static readonly StyledProperty<IBrush?> LineTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(LineToken));

    public static readonly StyledProperty<IBrush?> AccentTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(AccentToken));

    public static readonly StyledProperty<IBrush?> MutedTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(MutedToken));

    public static readonly StyledProperty<double> FontSizeTokenProperty =
        AvaloniaProperty.Register<DiagramView, double>(nameof(FontSizeToken), 15d);

    public DiagramView()
    {
        this.Apply(ForegroundTokenProperty, Themed.Keys.TextPrimary);
        this.Apply(BackgroundTokenProperty, "ContentBackgroundBrush");
        this.Apply(SurfaceTokenProperty, "DiagramSurfaceBrush");
        this.Apply(BorderTokenProperty, "DiagramBorderBrush");
        this.Apply(LineTokenProperty, "DiagramLineBrush");
        this.Apply(AccentTokenProperty, "AccentBrush");
        this.Apply(MutedTokenProperty, Themed.Keys.TextMuted);
        this.Apply(FontSizeTokenProperty, "BodyFontSize");
    }

    public string? Svg
    {
        get => GetValue(SvgProperty);
        set => SetValue(SvgProperty, value);
    }

    public string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    public IBrush? ForegroundToken
    {
        get => GetValue(ForegroundTokenProperty);
        set => SetValue(ForegroundTokenProperty, value);
    }

    public IBrush? BackgroundToken
    {
        get => GetValue(BackgroundTokenProperty);
        set => SetValue(BackgroundTokenProperty, value);
    }

    public IBrush? SurfaceToken
    {
        get => GetValue(SurfaceTokenProperty);
        set => SetValue(SurfaceTokenProperty, value);
    }

    public IBrush? BorderToken
    {
        get => GetValue(BorderTokenProperty);
        set => SetValue(BorderTokenProperty, value);
    }

    public IBrush? LineToken
    {
        get => GetValue(LineTokenProperty);
        set => SetValue(LineTokenProperty, value);
    }

    public IBrush? AccentToken
    {
        get => GetValue(AccentTokenProperty);
        set => SetValue(AccentTokenProperty, value);
    }

    public IBrush? MutedToken
    {
        get => GetValue(MutedTokenProperty);
        set => SetValue(MutedTokenProperty, value);
    }

    public double FontSizeToken
    {
        get => GetValue(FontSizeTokenProperty);
        set => SetValue(FontSizeTokenProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SvgProperty
            || change.Property == ErrorTextProperty
            || change.Property == ForegroundTokenProperty
            || change.Property == BackgroundTokenProperty
            || change.Property == SurfaceTokenProperty
            || change.Property == BorderTokenProperty
            || change.Property == LineTokenProperty
            || change.Property == AccentTokenProperty
            || change.Property == MutedTokenProperty
            || change.Property == FontSizeTokenProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (string.IsNullOrWhiteSpace(Svg))
        {
            Child = BuildError(string.IsNullOrWhiteSpace(ErrorText)
                ? "Diagram could not be rendered."
                : ErrorText!);
            return;
        }

        try
        {
            var flattened = SvgThemeFlattener.Flatten(Svg!, FontSizeToken);
            var source = Avalonia.Svg.Skia.SvgSource.LoadFromSvg(flattened);

            Child = new Image
            {
                Source = new Avalonia.Svg.Skia.SvgImage { Source = source },
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(14, 10, 14, 12)
            };
        }
        catch
        {
            Child = BuildError(string.IsNullOrWhiteSpace(ErrorText)
                ? "Diagram could not be rendered."
                : ErrorText!);
        }
    }

    private static Control BuildError(string text)
    {
        var error = new TextBlock
        {
            Text = text,
            Margin = new Thickness(14, 10, 14, 12),
            TextWrapping = TextWrapping.Wrap
        };

        error.Classes.Add("md-caption");
        return error;
    }
}
