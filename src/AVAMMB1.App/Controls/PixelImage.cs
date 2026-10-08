using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AVAMMB1.App.Controls;

/// <summary>Displays a bitmap scaled uniformly with nearest-neighbor filtering (crisp pixel art).</summary>
public sealed class PixelImage : Control
{
    /// <summary>The bitmap to show.</summary>
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<PixelImage, Bitmap?>(nameof(Source));

    static PixelImage()
    {
        AffectsRender<PixelImage>(SourceProperty);
        AffectsMeasure<PixelImage>(SourceProperty);
    }

    /// <summary>Bitmap.</summary>
    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) =>
        Source is null ? default : Source.Size.Constrain(availableSize);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Source is not { } bmp || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var src = bmp.Size;
        var scale = Math.Min(Bounds.Width / src.Width, Bounds.Height / src.Height);
        var w = src.Width * scale;
        var h = src.Height * scale;
        var dest = new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            context.DrawImage(bmp, new Rect(src), dest);
        }
    }
}
