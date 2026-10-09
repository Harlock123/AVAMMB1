using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAMMB1.App.ViewModels;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.App.Controls;

/// <summary>Draws the automap (explored cells, walls, doors, landmarks and the party arrow).</summary>
public sealed class MapView : Control
{
    /// <summary>Snapshot to draw.</summary>
    public static readonly StyledProperty<MapSnapshot?> MapInfoProperty =
        AvaloniaProperty.Register<MapView, MapSnapshot?>(nameof(MapInfo));

    /// <summary>Radius in cells around the party (0 = whole map).</summary>
    public static readonly StyledProperty<int> RadiusProperty =
        AvaloniaProperty.Register<MapView, int>(nameof(Radius));

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#0b0b12"));
    private static readonly IBrush Floor = new SolidColorBrush(Color.Parse("#2a2a3a"));
    private static readonly IBrush PartyBrush = new SolidColorBrush(Color.Parse("#ffd75e"));
    private static readonly IPen WallPen = new Pen(new SolidColorBrush(Color.Parse("#d8d0c0")), 2);
    private static readonly IPen DoorPen = new Pen(new SolidColorBrush(Color.Parse("#e09040")), 3);
    private static readonly IPen SecretPen = new Pen(new SolidColorBrush(Color.Parse("#c070ff")), 3);
    private static readonly IPen LockPen = new Pen(new SolidColorBrush(Color.Parse("#ff4040")), 3);
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#1c1c28")), 1);
    private static readonly Dictionary<string, IBrush> TerrainBrushes = new();

    static MapView()
    {
        AffectsRender<MapView>(MapInfoProperty, RadiusProperty);
    }

    /// <summary>Snapshot.</summary>
    public MapSnapshot? MapInfo
    {
        get => GetValue(MapInfoProperty);
        set => SetValue(MapInfoProperty, value);
    }

    /// <summary>Radius.</summary>
    public int Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    private static IBrush TerrainBrush(string hex)
    {
        if (!TerrainBrushes.TryGetValue(hex, out var b))
        {
            TerrainBrushes[hex] = b = new SolidColorBrush(Color.Parse(hex));
        }
        return b;
    }

    private static IBrush EventBrush(MapEventKind kind) => kind switch
    {
        MapEventKind.Shop or MapEventKind.Inn or MapEventKind.Temple or MapEventKind.Tavern or MapEventKind.Training => TerrainBrush("#5ec8ff"),
        MapEventKind.Teleport => TerrainBrush("#c070ff"),
        MapEventKind.Treasure => TerrainBrush("#ffd75e"),
        MapEventKind.Fountain => TerrainBrush("#40a0ff"),
        MapEventKind.Quest or MapEventKind.Victory => TerrainBrush("#ff70b0"),
        MapEventKind.Encounter => TerrainBrush("#ff5040"),
        _ => TerrainBrush("#c0c0c0"),
    };

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Background, bounds);
        if (MapInfo is not { } info)
        {
            return;
        }
        var session = info.Session;
        var map = session.CurrentMap;
        var state = session.State;
        int x0, y0, cols, rows;
        if (Radius > 0)
        {
            cols = rows = Radius * 2 + 1;
            x0 = state.X - Radius;
            y0 = state.Y - Radius;
        }
        else
        {
            x0 = y0 = 0;
            cols = map.Width;
            rows = map.Height;
        }
        var cell = Math.Floor(Math.Min(bounds.Width / cols, bounds.Height / rows));
        if (cell < 2)
        {
            return;
        }
        var ox = (bounds.Width - cell * cols) / 2;
        var oy = (bounds.Height - cell * rows) / 2;
        Rect CellRect(int x, int y) => new(ox + (x - x0) * cell, oy + (y - y0) * cell, cell, cell);

        for (var y = y0; y < y0 + rows; y++)
        {
            for (var x = x0; x < x0 + cols; x++)
            {
                if (!map.InBounds(x, y) || !state.IsExplored(map.Id, map.Width, x, y))
                {
                    continue;
                }
                var r = CellRect(x, y);
                var terrain = map.Terrain(x, y);
                var brush = terrain?.MapColor is { } c ? TerrainBrush(c) : map.IsSolid(x, y) ? TerrainBrush("#6b5b4b") : Floor;
                context.FillRectangle(brush, r.Deflate(map.IsSolid(x, y) ? 0 : 0.5));
                context.DrawRectangle(GridPen, r);
            }
        }

        // Walls and doors on explored cells.
        for (var y = y0; y < y0 + rows; y++)
        {
            for (var x = x0; x < x0 + cols; x++)
            {
                if (!map.InBounds(x, y) || !state.IsExplored(map.Id, map.Width, x, y))
                {
                    continue;
                }
                var r = CellRect(x, y);
                bool Found(Direction d) => state.IsSecretFound(map.Id, x, y, d);
                DrawEdge(context, map.GetWall(x, y, Direction.North), Found(Direction.North), r.TopLeft, r.TopRight);
                DrawEdge(context, map.GetWall(x, y, Direction.West), Found(Direction.West), r.TopLeft, r.BottomLeft);
                if (y == map.Height - 1 || !state.IsExplored(map.Id, map.Width, x, y + 1))
                {
                    DrawEdge(context, map.GetWall(x, y, Direction.South), Found(Direction.South), r.BottomLeft, r.BottomRight);
                }
                if (x == map.Width - 1 || !state.IsExplored(map.Id, map.Width, x + 1, y))
                {
                    DrawEdge(context, map.GetWall(x, y, Direction.East), Found(Direction.East), r.TopRight, r.BottomRight);
                }
            }
        }

        foreach (var ev in map.AllEvents)
        {
            if (ev.X < x0 || ev.Y < y0 || ev.X >= x0 + cols || ev.Y >= y0 + rows || !state.IsExplored(map.Id, map.Width, ev.X, ev.Y))
            {
                continue;
            }
            if (ev.Type is MapEventKind.Trap or MapEventKind.Encounter || (ev.Type == MapEventKind.Message && ev.Feature is null))
            {
                continue;
            }
            var r = CellRect(ev.X, ev.Y);
            var d = Math.Max(3, cell * 0.35);
            context.DrawEllipse(EventBrush(ev.Type), null, r.Center, d / 2, d / 2);
        }

        // Party arrow.
        var pr = CellRect(state.X, state.Y);
        var c0 = pr.Center;
        var s = cell * 0.38;
        var (dx, dy) = (state.Facing.Dx(), state.Facing.Dy());
        var tip = new Point(c0.X + dx * s, c0.Y + dy * s);
        var left = new Point(c0.X - dx * s * 0.6 + dy * s * 0.7, c0.Y - dy * s * 0.6 - dx * s * 0.7);
        var right = new Point(c0.X - dx * s * 0.6 - dy * s * 0.7, c0.Y - dy * s * 0.6 + dx * s * 0.7);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(tip, true);
            g.LineTo(left);
            g.LineTo(right);
            g.EndFigure(true);
        }
        context.DrawGeometry(PartyBrush, new Pen(Brushes.Black, 1), geo);
    }

    private static void DrawEdge(DrawingContext ctx, WallKind kind, bool secretFound, Point a, Point b)
    {
        var pen = kind switch
        {
            WallKind.SecretDoor => secretFound ? SecretPen : WallPen,
            WallKind.Wall => WallPen,
            WallKind.Door => DoorPen,
            WallKind.LockedDoor => LockPen,
            _ => null,
        };
        if (pen is not null)
        {
            ctx.DrawLine(pen, a, b);
        }
    }
}
