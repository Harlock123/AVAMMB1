using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.App.Rendering;

/// <summary>A billboard sprite placed at a cell center.</summary>
/// <param name="X">Cell X.</param>
/// <param name="Y">Cell Y.</param>
/// <param name="Texture">Feature texture.</param>
/// <param name="Scale">Height relative to a wall (1 = full height).</param>
/// <param name="Bob">Idle bobbing amplitude as a fraction of the sprite height (0 = static).</param>
public readonly record struct SceneSprite(int X, int Y, Texture Texture, double Scale = 0.5, double Bob = 0);

/// <summary>A free camera: position in cell units (cell centers are at +0.5) and view angle in radians
/// (0 = east, pi/2 = south, since y grows southwards).</summary>
/// <param name="X">X position.</param>
/// <param name="Y">Y position.</param>
/// <param name="Angle">View angle.</param>
public readonly record struct Camera(double X, double Y, double Angle)
{
    /// <summary>The camera standing in the center of a cell, looking along a grid direction.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    /// <param name="facing">Facing.</param>
    public static Camera At(int x, int y, Direction facing) => new(x + 0.5, y + 0.5, Math.Atan2(facing.Dy(), facing.Dx()));

    /// <summary>Interpolates between two cameras, turning the short way round.</summary>
    /// <param name="a">Start.</param>
    /// <param name="b">End.</param>
    /// <param name="t">0..1.</param>
    public static Camera Lerp(Camera a, Camera b, double t)
    {
        var turn = Math.IEEERemainder(b.Angle - a.Angle, 2 * Math.PI);
        return new Camera(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Angle + turn * t);
    }
}

/// <summary>Everything needed to draw one frame of the first-person view.</summary>
public sealed class SceneDescription
{
    /// <summary>The map.</summary>
    public required GameMap Map { get; init; }
    /// <summary>Party X (cell).</summary>
    public required int X { get; init; }
    /// <summary>Party Y (cell).</summary>
    public required int Y { get; init; }
    /// <summary>Facing.</summary>
    public required Direction Facing { get; init; }
    /// <summary>Whether the party is in darkness (no usable light): everything beyond arm's reach is black.</summary>
    public bool Dark { get; init; }
    /// <summary>Visible distance in cells.</summary>
    public required int ViewDistance { get; init; }
    /// <summary>Whether the secret door on a side of a cell has been discovered (undiscovered ones draw as walls).</summary>
    public Func<int, int, Direction, bool> SecretFound { get; init; } = (_, _, _) => false;
    /// <summary>Feature billboards.</summary>
    public IReadOnlyList<SceneSprite> Sprites { get; init; } = Array.Empty<SceneSprite>();
}

/// <summary>
/// Software ray caster producing a classic pseudo-3D grid view. Supports both thin walls between
/// cells (doors, secret doors) and solid blocks, textured floors/ceilings, sky, fog and billboards.
/// </summary>
/// <param name="textures">Texture source.</param>
public sealed class SceneRenderer(TextureCache textures)
{
    /// <summary>Framebuffer width.</summary>
    public const int Width = 400;
    /// <summary>Framebuffer height.</summary>
    public const int Height = 300;

    private const double PlaneLength = 0.66;
    private readonly double[] _zBuffer = new double[Width];

    private static readonly Dictionary<string, uint> Backings = new()
    {
        ["tree"] = 0xFF2D5A27,
        ["water"] = 0xFF1E3F7A,
    };

    /// <summary>Animation clock in seconds (drives sprite bobbing).</summary>
    public double Time { get; set; }

    /// <summary>The pixel buffer (0xAARRGGBB), Width * Height.</summary>
    public uint[] Pixels { get; } = new uint[Width * Height];

    private Texture Tex(string key) => textures.Get("Textures/" + key, Backings.GetValueOrDefault(key));

    private static uint ParseColor(string hex, uint fallback)
    {
        var s = hex.TrimStart('#');
        if (s.Length == 3)
        {
            s = string.Concat(s.Select(c => new string(c, 2)));
        }
        return s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? 0xFF000000u | v : fallback;
    }

    private static uint Shade(uint c, double light, uint fog, double fogAmount)
    {
        var r = ((c >> 16) & 0xFF) * light;
        var g = ((c >> 8) & 0xFF) * light;
        var b = (c & 0xFF) * light;
        if (fogAmount > 0)
        {
            r += (((fog >> 16) & 0xFF) - r) * fogAmount;
            g += (((fog >> 8) & 0xFF) - g) * fogAmount;
            b += ((fog & 0xFF) - b) * fogAmount;
        }
        return 0xFF000000u | ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);
    }

    /// <summary>Renders a frame into <see cref="Pixels"/>.</summary>
    /// <param name="scene">What to draw.</param>
    /// <param name="camera">Camera override (used for movement animation); defaults to the party's cell and facing.</param>
    public void Render(SceneDescription scene, Camera? camera = null)
    {
        var map = scene.Map;
        var def = map.Def;
        var cam = camera ?? Camera.At(scene.X, scene.Y, scene.Facing);
        var px = cam.X;
        var py = cam.Y;
        double dirX = Math.Cos(cam.Angle), dirY = Math.Sin(cam.Angle);
        // Snap tiny floating point noise so grid-aligned views stay pixel-identical to before.
        if (Math.Abs(dirX) < 1e-9) dirX = 0;
        if (Math.Abs(dirY) < 1e-9) dirY = 0;
        var planeX = -dirY * PlaneLength;
        var planeY = dirX * PlaneLength;

        var dark = scene.Dark || (def.Dark && scene.ViewDistance <= 1);
        var skyTop = ParseColor(def.SkyColor, 0xFF3B6FB6);
        var horizon = Shade(skyTop, 1.0, 0xFFE8EEF5, 0.55);
        var fog = def.Kind == MapKind.Dungeon || dark ? 0xFF000000u : horizon;
        var fogDist = dark ? 1.7 : scene.ViewDistance + 0.8;
        var maxDist = Math.Max(2.0, fogDist + 0.5);

        DrawFloorAndCeiling(map, px, py, dirX, dirY, planeX, planeY, fog, fogDist, skyTop, horizon, dark);

        for (var x = 0; x < Width; x++)
        {
            var cameraX = 2.0 * x / Width - 1;
            var rdx = dirX + planeX * cameraX;
            var rdy = dirY + planeY * cameraX;
            var mapX = (int)Math.Floor(px);
            var mapY = (int)Math.Floor(py);
            var deltaX = rdx == 0 ? 1e30 : Math.Abs(1 / rdx);
            var deltaY = rdy == 0 ? 1e30 : Math.Abs(1 / rdy);
            int stepX, stepY;
            double sideX, sideY;
            if (rdx < 0) { stepX = -1; sideX = (px - mapX) * deltaX; } else { stepX = 1; sideX = (mapX + 1 - px) * deltaX; }
            if (rdy < 0) { stepY = -1; sideY = (py - mapY) * deltaY; } else { stepY = 1; sideY = (mapY + 1 - py) * deltaY; }

            Texture? hitTex = null;
            double dist = maxDist;
            var side = 0;
            for (var guard = 0; guard < 64; guard++)
            {
                double d;
                WallKind wall;
                Direction side0;
                int fromX, fromY;
                if (sideX < sideY)
                {
                    d = sideX;
                    sideX += deltaX;
                    side = 0;
                    side0 = stepX > 0 ? Direction.East : Direction.West;
                    wall = map.GetWall(mapX, mapY, side0);
                    fromX = mapX;
                    fromY = mapY;
                    mapX += stepX;
                }
                else
                {
                    d = sideY;
                    sideY += deltaY;
                    side = 1;
                    side0 = stepY > 0 ? Direction.South : Direction.North;
                    wall = map.GetWall(mapX, mapY, side0);
                    fromX = mapX;
                    fromY = mapY;
                    mapY += stepY;
                }
                if (d > maxDist)
                {
                    break;
                }
                if (wall != WallKind.None)
                {
                    hitTex = wall switch
                    {
                        WallKind.Door => Tex("door"),
                        WallKind.LockedDoor => Tex("door_locked"),
                        WallKind.SecretDoor when scene.SecretFound(fromX, fromY, side0) => Tex("door"),
                        _ => Tex(def.WallTexture),
                    };
                    dist = d;
                    break;
                }
                if (map.IsOpaque(mapX, mapY))
                {
                    hitTex = map.InBounds(mapX, mapY) ? Tex(map.SolidTexture(mapX, mapY)) : Tex(def.WallTexture);
                    dist = d;
                    break;
                }
            }

            _zBuffer[x] = dist;
            if (hitTex is null)
            {
                continue;
            }
            dist = Math.Max(dist, 0.05);
            var wallX = side == 0 ? py + dist * rdy : px + dist * rdx;
            wallX -= Math.Floor(wallX);
            var texX = (int)(wallX * hitTex.Width);
            if ((side == 0 && rdx > 0) || (side == 1 && rdy < 0))
            {
                texX = hitTex.Width - texX - 1;
            }
            var lineHeight = (int)(Height / dist);
            var start = Height / 2 - lineHeight / 2;
            var light = side == 1 ? 0.78 : 1.0;
            var fogAmount = FogAmount(dist, fogDist, dark);
            var y0 = Math.Max(0, start);
            var y1 = Math.Min(Height - 1, start + lineHeight);
            for (var y = y0; y <= y1; y++)
            {
                var texY = (int)((long)(y - start) * hitTex.Height / Math.Max(1, lineHeight));
                Pixels[y * Width + x] = Shade(hitTex.Sample(texX, texY), light, fog, fogAmount);
            }
        }

        DrawSprites(scene, px, py, dirX, dirY, planeX, planeY, fog, fogDist, dark);
    }

    private static double FogAmount(double dist, double fogDist, bool dark)
    {
        var f = Math.Clamp(dist / fogDist, 0, 1);
        return dark ? Math.Min(1, f * f * 1.1) : Math.Pow(f, 2.2);
    }

    private void DrawFloorAndCeiling(GameMap map, double px, double py, double dirX, double dirY, double planeX, double planeY,
        uint fog, double fogDist, uint skyTop, uint horizon, bool dark)
    {
        var def = map.Def;
        var ceiling = def.CeilingTexture is null ? null : Tex(def.CeilingTexture);
        var floorCache = new Dictionary<string, Texture>();
        var rdx0 = dirX - planeX;
        var rdy0 = dirY - planeY;
        var rdx1 = dirX + planeX;
        var rdy1 = dirY + planeY;
        var half = Height / 2;
        Array.Fill(Pixels, fog);

        if (ceiling is null && !dark)
        {
            for (var y = 0; y < half; y++)
            {
                var t = (double)y / half;
                var c = Shade(skyTop, 1.0, horizon, t * t);
                Array.Fill(Pixels, c, y * Width, Width);
            }
        }

        for (var y = half + 1; y < Height; y++)
        {
            var p = y - half;
            var rowDist = 0.5 * Height / p;
            var fogAmount = FogAmount(rowDist, fogDist, dark);
            var stepX = rowDist * (rdx1 - rdx0) / Width;
            var stepY = rowDist * (rdy1 - rdy0) / Width;
            var fx = px + rowDist * rdx0;
            var fy = py + rowDist * rdy0;
            var cy = Height - y - 1;
            for (var x = 0; x < Width; x++)
            {
                var cellX = (int)Math.Floor(fx);
                var cellY = (int)Math.Floor(fy);
                if (fogAmount >= 0.999)
                {
                    Pixels[y * Width + x] = fog;
                    if (ceiling is not null)
                    {
                        Pixels[cy * Width + x] = fog;
                    }
                }
                else
                {
                    var key = map.InBounds(cellX, cellY) ? map.FloorTexture(cellX, cellY) : def.FloorTexture;
                    if (!floorCache.TryGetValue(key, out var ft))
                    {
                        floorCache[key] = ft = Tex(key);
                    }
                    var tx = (int)(ft.Width * (fx - cellX));
                    var ty = (int)(ft.Height * (fy - cellY));
                    Pixels[y * Width + x] = Shade(ft.Sample(tx, ty), 0.9, fog, fogAmount);
                    if (ceiling is not null)
                    {
                        var ctx = (int)(ceiling.Width * (fx - cellX));
                        var cty = (int)(ceiling.Height * (fy - cellY));
                        Pixels[cy * Width + x] = Shade(ceiling.Sample(ctx, cty), 0.55, fog, fogAmount);
                    }
                }
                fx += stepX;
                fy += stepY;
            }
        }
    }

    private void DrawSprites(SceneDescription scene, double px, double py, double dirX, double dirY, double planeX, double planeY,
        uint fog, double fogDist, bool dark)
    {
        var invDet = 1.0 / (planeX * dirY - dirX * planeY);
        var list = scene.Sprites
            .Select(s => (Sprite: s, Rx: s.X + 0.5 - px, Ry: s.Y + 0.5 - py))
            .OrderByDescending(s => s.Rx * s.Rx + s.Ry * s.Ry);
        foreach (var (sprite, rx, ry) in list)
        {
            var tX = invDet * (dirY * rx - dirX * ry);
            var tY = invDet * (-planeY * rx + planeX * ry);
            if (tY <= 0.3 || tY > fogDist + 0.5)
            {
                continue;
            }
            var screenX = (int)(Width / 2.0 * (1 + tX / tY));
            var size = (int)Math.Abs(Height / tY * sprite.Scale);
            var floorY = (int)(Height / 2.0 + Height / (2.0 * tY));
            if (sprite.Bob > 0)
            {
                // Each sprite gets its own phase so neighbours don't bob in lock-step.
                var phase = (sprite.X * 7 + sprite.Y * 13) % 10 / 10.0 * 2 * Math.PI;
                floorY -= (int)((Math.Sin(Time * 2 * Math.PI / 1.7 + phase) * 0.5 + 0.5) * sprite.Bob * size);
            }
            var top = floorY - size;
            var left = screenX - size / 2;
            var fogAmount = FogAmount(tY, fogDist, dark);
            var tex = sprite.Texture;
            for (var sx = Math.Max(0, left); sx < Math.Min(Width, left + size); sx++)
            {
                if (tY >= _zBuffer[sx])
                {
                    continue;
                }
                var texX = (sx - left) * tex.Width / size;
                for (var sy = Math.Max(0, top); sy < Math.Min(Height, floorY); sy++)
                {
                    var texY = (sy - top) * tex.Height / size;
                    var c = tex.Sample(texX, texY);
                    if ((c >> 24) < 128)
                    {
                        continue;
                    }
                    Pixels[sy * Width + sx] = Shade(c, 1.0, fog, fogAmount);
                }
            }
        }
    }
}
