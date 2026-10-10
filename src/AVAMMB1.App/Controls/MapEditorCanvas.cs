using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.World;

namespace AVAMMB1.App.Controls;

/// <summary>
/// The map editor's drawing: squares (floor, rock, darkness, anti-magic), the walls and doors between them,
/// events, the test-play start and the selected square. Clicking near a square's side edits that side;
/// clicking its middle edits the square. Dragging paints.
/// </summary>
public sealed class MapEditorCanvas : Control
{
    /// <summary>The map being edited.</summary>
    public static readonly StyledProperty<MapDraft?> DraftProperty = AvaloniaProperty.Register<MapEditorCanvas, MapDraft?>(nameof(Draft));

    /// <summary>Changes whenever the draft does (to redraw).</summary>
    public static readonly StyledProperty<int> VersionProperty = AvaloniaProperty.Register<MapEditorCanvas, int>(nameof(Version));

    /// <summary>The selected square.</summary>
    public static readonly StyledProperty<PixelPoint> SelectedProperty = AvaloniaProperty.Register<MapEditorCanvas, PixelPoint>(nameof(Selected), new PixelPoint(-1, -1));

    /// <summary>Where a test play starts.</summary>
    public static readonly StyledProperty<PixelPoint> StartProperty = AvaloniaProperty.Register<MapEditorCanvas, PixelPoint>(nameof(Start), new PixelPoint(-1, -1));

    static MapEditorCanvas()
    {
        AffectsRender<MapEditorCanvas>(DraftProperty, VersionProperty, SelectedProperty, StartProperty);
    }

    /// <summary>Creates the canvas.</summary>
    public MapEditorCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>The map being edited.</summary>
    public MapDraft? Draft { get => GetValue(DraftProperty); set => SetValue(DraftProperty, value); }

    /// <summary>Redraw counter.</summary>
    public int Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    /// <summary>The selected square.</summary>
    public PixelPoint Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    /// <summary>Test-play start square.</summary>
    public PixelPoint Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>A square (and the side near the pointer, if any) was clicked or painted over; the flag says whether it is a drag.</summary>
    public event Action<int, int, CellSide?, bool>? CellClicked;

    private (int X, int Y, CellSide? Side)? _last;

    private (double Cell, double Ox, double Oy) Layout(MapDraft d)
    {
        var cell = Math.Floor(Math.Min((Bounds.Width - 8) / d.Width, (Bounds.Height - 8) / d.Height));
        cell = Math.Max(4, cell);
        return (cell, (Bounds.Width - cell * d.Width) / 2, (Bounds.Height - cell * d.Height) / 2);
    }

    private (int X, int Y, CellSide? Side)? Hit(Point p)
    {
        if (Draft is not { } d)
        {
            return null;
        }
        var (cell, ox, oy) = Layout(d);
        var fx = (p.X - ox) / cell;
        var fy = (p.Y - oy) / cell;
        var x = (int)Math.Floor(fx);
        var y = (int)Math.Floor(fy);
        if (!d.InBounds(x, y))
        {
            return null;
        }
        var (dx, dy) = (fx - x, fy - y);
        const double near = 0.28;
        var best = new[] { (dy, CellSide.North), (1 - dx, CellSide.East), (1 - dy, CellSide.South), (dx, CellSide.West) }.MinBy(t => t.Item1);
        return (x, y, best.Item1 < near ? best.Item2 : null);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (Hit(e.GetPosition(this)) is { } hit)
        {
            _last = hit;
            CellClicked?.Invoke(hit.X, hit.Y, hit.Side, false);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Hit(e.GetPosition(this)) is not { } hit || hit == _last)
        {
            return;
        }
        _last = hit;
        CellClicked?.Invoke(hit.X, hit.Y, hit.Side, true);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _last = null;
    }

    private static readonly IBrush Floor = new SolidColorBrush(Color.Parse("#2a2833"));
    private static readonly IBrush Rock = new SolidColorBrush(Color.Parse("#5a4a3a"));
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#14121c"));
    private static readonly IBrush AntiMagic = new SolidColorBrush(Color.Parse("#3a2850"));
    private static readonly IBrush Other = new SolidColorBrush(Color.Parse("#2f4a3a"));
    private static readonly IPen Grid = new Pen(new SolidColorBrush(Color.Parse("#3a3846")), 1);
    private static readonly IPen Wall = new Pen(new SolidColorBrush(Color.Parse("#e8d9b0")), 3, lineCap: PenLineCap.Round);
    private static readonly IPen Door = new Pen(new SolidColorBrush(Color.Parse("#e8913a")), 4, lineCap: PenLineCap.Round);
    private static readonly IPen Locked = new Pen(new SolidColorBrush(Color.Parse("#e04848")), 4, lineCap: PenLineCap.Round);
    private static readonly IPen Secret = new Pen(new SolidColorBrush(Color.Parse("#a070e0")), 4, new DashStyle([1.5, 1.5], 0));
    private static readonly IPen SelectedPen = new Pen(new SolidColorBrush(Color.Parse("#ffd75e")), 2);
    private static readonly IPen StartPen = new Pen(new SolidColorBrush(Color.Parse("#5ee0ff")), 2);

    private static IBrush EventBrush(MapEventKind kind) => new SolidColorBrush(Color.Parse(kind switch
    {
        MapEventKind.Encounter or MapEventKind.Trap => "#e04848",
        MapEventKind.Treasure or MapEventKind.Quest => "#f0c040",
        MapEventKind.Teleport => "#b070f0",
        MapEventKind.Shop or MapEventKind.Inn or MapEventKind.Temple or MapEventKind.Tavern or MapEventKind.Training or MapEventKind.Academy => "#40b0f0",
        MapEventKind.Riddle or MapEventKind.Choice => "#f080c0",
        MapEventKind.Fountain => "#40e0c0",
        _ => "#c0c0c0",
    }));

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        if (Draft is not { } d)
        {
            return;
        }
        var (cell, ox, oy) = Layout(d);
        Rect R(int x, int y) => new(ox + x * cell, oy + y * cell, cell, cell);
        for (var x = 0; x < d.Width; x++)
        {
            for (var y = 0; y < d.Height; y++)
            {
                var ch = d.Cell(x, y);
                var solid = ch == '#' || (d.Meta.Terrain.TryGetValue(ch.ToString(), out var t) && t.Solid);
                var brush = ch switch
                {
                    '.' => Floor,
                    'd' => Dark,
                    'a' => AntiMagic,
                    _ when solid => Rock,
                    _ => Other,
                };
                context.FillRectangle(brush, R(x, y));
                context.DrawRectangle(Grid, R(x, y));
            }
        }
        for (var x = 0; x < d.Width; x++)
        {
            for (var y = 0; y < d.Height; y++)
            {
                var r = R(x, y);
                DrawEdge(context, d.Edge(x, y, CellSide.North), r.TopLeft, r.TopRight);
                DrawEdge(context, d.Edge(x, y, CellSide.West), r.TopLeft, r.BottomLeft);
                if (y == d.Height - 1)
                {
                    DrawEdge(context, d.Edge(x, y, CellSide.South), r.BottomLeft, r.BottomRight);
                }
                if (x == d.Width - 1)
                {
                    DrawEdge(context, d.Edge(x, y, CellSide.East), r.TopRight, r.BottomRight);
                }
            }
        }
        foreach (var group in d.Events.GroupBy(e => (e.X, e.Y)))
        {
            var r = R(group.Key.X, group.Key.Y);
            var i = 0;
            foreach (var e in group.Take(4))
            {
                var size = Math.Max(3, cell * 0.22);
                var cx = r.X + cell * (0.3 + 0.4 * (i % 2));
                var cy = r.Y + cell * (0.3 + 0.4 * (i / 2));
                context.DrawEllipse(EventBrush(e.Type), null, new Point(cx, cy), size, size);
                i++;
            }
        }
        if (d.InBounds(Start.X, Start.Y))
        {
            var r = R(Start.X, Start.Y).Deflate(cell * 0.18);
            context.DrawLine(StartPen, r.BottomLeft, new Point(r.Center.X, r.Top));
            context.DrawLine(StartPen, new Point(r.Center.X, r.Top), r.BottomRight);
        }
        if (d.InBounds(Selected.X, Selected.Y))
        {
            context.DrawRectangle(SelectedPen, R(Selected.X, Selected.Y).Deflate(1));
        }
    }

    private static void DrawEdge(DrawingContext context, char edge, Point a, Point b)
    {
        var pen = edge switch
        {
            '-' or '|' => Wall,
            'D' => Door,
            'L' => Locked,
            'S' => Secret,
            _ => null,
        };
        if (pen is not null)
        {
            context.DrawLine(pen, a, b);
        }
    }
}
