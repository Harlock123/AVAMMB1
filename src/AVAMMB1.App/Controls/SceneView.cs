using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AVAMMB1.App.Rendering;

namespace AVAMMB1.App.Controls;

/// <summary>Displays the software-rendered first-person view, scaled with nearest-neighbor filtering.</summary>
public sealed class SceneView : Control
{
    /// <summary>The scene to draw.</summary>
    public static readonly StyledProperty<SceneDescription?> SceneProperty =
        AvaloniaProperty.Register<SceneView, SceneDescription?>(nameof(Scene));

    private readonly WriteableBitmap _bitmap = new(new PixelSize(SceneRenderer.Width, SceneRenderer.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private SceneRenderer? _renderer;

    static SceneView()
    {
        AffectsRender<SceneView>(SceneProperty);
    }

    /// <summary>Scene.</summary>
    public SceneDescription? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty && Scene is { } scene)
        {
            _renderer ??= new SceneRenderer(App.Textures);
            _renderer.Render(scene);
            using var fb = _bitmap.Lock();
            var px = _renderer.Pixels;
            unsafe
            {
                for (var y = 0; y < SceneRenderer.Height; y++)
                {
                    var dst = new Span<uint>((void*)(fb.Address + y * fb.RowBytes), SceneRenderer.Width);
                    px.AsSpan(y * SceneRenderer.Width, SceneRenderer.Width).CopyTo(dst);
                }
            }
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Black, bounds);
        if (Scene is null)
        {
            return;
        }
        var scale = Math.Min(bounds.Width / SceneRenderer.Width, bounds.Height / SceneRenderer.Height);
        var w = SceneRenderer.Width * scale;
        var h = SceneRenderer.Height * scale;
        var dest = new Rect((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            context.DrawImage(_bitmap, new Rect(0, 0, SceneRenderer.Width, SceneRenderer.Height), dest);
        }
    }
}
