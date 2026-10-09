using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AVAMMB1.App.Rendering;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.App.Controls;

/// <summary>Displays the software-rendered first-person view, scaled with nearest-neighbor filtering.</summary>
public sealed class SceneView : Control
{
    /// <summary>The scene to draw.</summary>
    public static readonly StyledProperty<SceneDescription?> SceneProperty =
        AvaloniaProperty.Register<SceneView, SceneDescription?>(nameof(Scene));

    /// <summary>Whether steps and turns are animated instead of snapping.</summary>
    public static readonly StyledProperty<bool> AnimateProperty =
        AvaloniaProperty.Register<SceneView, bool>(nameof(Animate), true);

    /// <summary>Duration of one step.</summary>
    public static readonly TimeSpan StepDuration = TimeSpan.FromMilliseconds(170);

    /// <summary>Duration of one 90-degree turn.</summary>
    public static readonly TimeSpan TurnDuration = TimeSpan.FromMilliseconds(150);

    private readonly WriteableBitmap _bitmap = new(new PixelSize(SceneRenderer.Width, SceneRenderer.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private SceneRenderer? _renderer;
    private SceneDescription? _shown;
    private Camera _from;
    private Camera _to;
    private TimeSpan _duration;
    private int _animation;
    private bool _animating;

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

    /// <summary>Animate steps and turns.</summary>
    public bool Animate
    {
        get => GetValue(AnimateProperty);
        set => SetValue(AnimateProperty, value);
    }

    /// <summary>Whether a step or turn animation is playing.</summary>
    public bool IsMoving => _animating;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SceneProperty || Scene is not { } scene)
        {
            return;
        }
        var target = Camera.At(scene.X, scene.Y, scene.Facing);
        var previous = _shown;
        _shown = scene;

        if (_animating && target == _to)
        {
            return; // same destination (e.g. light or automap changed): keep animating with the new scene data
        }
        var start = previous is null ? target : Camera.At(previous.X, previous.Y, previous.Facing);
        if (Animate && previous is not null && ReferenceEquals(previous.Map, scene.Map) && IsSingleMove(previous, scene)
            && TopLevel.GetTopLevel(this) is { } top)
        {
            // A new move while animating starts from the previous destination: the old move completes instantly.
            _from = start;
            _to = target;
            _duration = previous.Facing != scene.Facing ? TurnDuration : StepDuration;
            _animating = true;
            var id = ++_animation;
            _clock.Restart();
            Draw(scene, _from);
            top.RequestAnimationFrame(_ => OnFrame(id));
            return;
        }
        _animating = false;
        _animation++;
        Draw(scene, target);
    }

    private static bool IsSingleMove(SceneDescription a, SceneDescription b)
    {
        var steps = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        var turned = a.Facing != b.Facing;
        return (steps == 1 && !turned) || (steps == 0 && turned && a.Facing.Opposite() != b.Facing);
    }

    private void OnFrame(int id)
    {
        if (id != _animation || _shown is not { } scene)
        {
            return; // superseded by a newer move
        }
        var t = Math.Min(1.0, _clock.Elapsed.TotalMilliseconds / _duration.TotalMilliseconds);
        var eased = t * t * (3 - 2 * t); // smoothstep: gentle start and stop
        Draw(scene, Camera.Lerp(_from, _to, eased));
        if (t >= 1)
        {
            _animating = false;
            return;
        }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ => OnFrame(id));
    }

    private void Draw(SceneDescription scene, Camera camera)
    {
        _renderer ??= new SceneRenderer(App.Textures);
        _renderer.Render(scene, camera);
        using (var fb = _bitmap.Lock())
        {
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
        InvalidateVisual();
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
