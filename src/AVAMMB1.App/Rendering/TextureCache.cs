using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AVAMMB1.App.Rendering;

/// <summary>A decoded texture as packed 0xAARRGGBB pixels.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Pixels">Row-major pixels.</param>
public sealed record Texture(int Width, int Height, uint[] Pixels)
{
    /// <summary>Samples with wrapping.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public uint Sample(int x, int y)
    {
        x %= Width;
        y %= Height;
        if (x < 0) x += Width;
        if (y < 0) y += Height;
        return Pixels[y * Width + x];
    }

    private Texture? _half;

    /// <summary>
    /// The texture to sample when it covers about <paramref name="screenPixels"/> pixels on screen: a
    /// box-filtered half-size copy (repeatedly) for large textures far away, so detailed textures do not
    /// shimmer. Textures of 32 pixels or less are never reduced (the classic look stays identical).
    /// </summary>
    /// <param name="screenPixels">On-screen size of one texture repeat.</param>
    public Texture ForSize(double screenPixels)
    {
        var t = this;
        while (t.Width > 32 && t.Height > 32 && t.Width / 2 >= screenPixels)
        {
            t = t._half ??= t.Halve();
        }
        return t;
    }

    private Texture Halve()
    {
        int w = Width / 2, h = Height / 2;
        var px = new uint[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                uint a = 0, r = 0, g = 0, b = 0;
                foreach (var c in new[] { Pixels[2 * y * Width + 2 * x], Pixels[2 * y * Width + 2 * x + 1], Pixels[(2 * y + 1) * Width + 2 * x], Pixels[(2 * y + 1) * Width + 2 * x + 1] })
                {
                    a += c >> 24;
                    r += (c >> 16) & 0xFF;
                    g += (c >> 8) & 0xFF;
                    b += c & 0xFF;
                }
                px[y * w + x] = (a / 4) << 24 | (r / 4) << 16 | (g / 4) << 8 | (b / 4);
            }
        }
        return new Texture(w, h, px);
    }
}

/// <summary>Loads and caches images from the embedded <c>Assets/Graphics</c> folder.</summary>
public sealed class TextureCache
{
    private readonly Dictionary<string, Texture> _textures = new();
    private readonly Dictionary<string, Bitmap?> _bitmaps = new();
    private readonly Dictionary<string, bool> _hasDetailed = new();

    /// <summary>
    /// Use the detailed (128 x 128) wall and floor textures from <c>Graphics/TexturesHD</c> where they
    /// exist; others keep their classic tile.
    /// </summary>
    public bool Detailed { get; set; }

    /// <summary>The path actually used for a texture, honouring <see cref="Detailed"/>.</summary>
    /// <param name="path">Requested path (e.g. <c>Textures/wall_crypt</c>).</param>
    public string Resolve(string path)
    {
        if (!Detailed || !path.StartsWith("Textures/", StringComparison.Ordinal))
        {
            return path;
        }
        var hd = "TexturesHD/" + path["Textures/".Length..];
        if (!_hasDetailed.TryGetValue(hd, out var exists))
        {
            _hasDetailed[hd] = exists = AssetLoader.Exists(UriFor(hd));
        }
        return exists ? hd : path;
    }

    /// <summary>Fallback texture used when an asset is missing (magenta checkerboard).</summary>
    public static Texture Missing { get; } = MakeChecker();

    private static Texture MakeChecker()
    {
        var px = new uint[32 * 32];
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                px[y * 32 + x] = ((x / 8 + y / 8) & 1) == 0 ? 0xFFFF00FFu : 0xFF202020u;
            }
        }
        return new Texture(32, 32, px);
    }

    private static Uri UriFor(string path) => new($"avares://AVAMMB1/Assets/Graphics/{path}.png");

    /// <summary>Gets an Avalonia bitmap for UI use (e.g. <c>Monsters/goblin</c>), or null when missing.</summary>
    /// <param name="path">Path below Assets/Graphics without extension.</param>
    public Bitmap? Bitmap(string path)
    {
        if (_bitmaps.TryGetValue(path, out var bmp))
        {
            return bmp;
        }
        try
        {
            using var s = AssetLoader.Open(UriFor(path));
            bmp = new Bitmap(s);
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or ArgumentException or InvalidOperationException)
        {
            bmp = null;
        }
        _bitmaps[path] = bmp;
        return bmp;
    }

    /// <summary>Gets raw pixels for the software renderer.</summary>
    /// <param name="path">Path below Assets/Graphics without extension.</param>
    /// <param name="backing">If non-zero, transparent pixels are composited onto this opaque color.</param>
    public Texture Get(string path, uint backing = 0)
    {
        path = Resolve(path);
        var key = backing == 0 ? path : $"{path}@{backing:X8}";
        if (_textures.TryGetValue(key, out var tex))
        {
            return tex;
        }
        var bmp = Bitmap(path);
        tex = bmp is null ? Missing : Decode(bmp);
        if (backing != 0 && !ReferenceEquals(tex, Missing))
        {
            var px = (uint[])tex.Pixels.Clone();
            for (var i = 0; i < px.Length; i++)
            {
                px[i] = Blend(backing, px[i]);
            }
            tex = tex with { Pixels = px };
        }
        _textures[key] = tex;
        return tex;
    }

    /// <summary>Builds a character portrait from a base body plus paper-doll layers (cached).</summary>
    /// <param name="basePath">Base image path (e.g. <c>Portraits/elf_female</c>).</param>
    /// <param name="layers">Layer paths drawn in order.</param>
    public Bitmap? Composite(string basePath, IReadOnlyList<string> layers)
    {
        var key = basePath + "|" + string.Join("|", layers);
        if (_bitmaps.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var baseTex = Get(basePath);
        var px = (uint[])baseTex.Pixels.Clone();
        foreach (var layer in layers)
        {
            var t = Get(layer);
            if (ReferenceEquals(t, Missing) || t.Width != baseTex.Width || t.Height != baseTex.Height)
            {
                continue;
            }
            for (var i = 0; i < px.Length; i++)
            {
                var src = t.Pixels[i];
                var a = src >> 24;
                if (a == 0)
                {
                    continue;
                }
                px[i] = a == 255 || (px[i] >> 24) == 0 ? src : Blend(px[i] | 0xFF000000u, src);
            }
        }
        var wb = new WriteableBitmap(new PixelSize(baseTex.Width, baseTex.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = wb.Lock())
        {
            unsafe
            {
                for (var y = 0; y < baseTex.Height; y++)
                {
                    px.AsSpan(y * baseTex.Width, baseTex.Width).CopyTo(new Span<uint>((void*)(fb.Address + y * fb.RowBytes), baseTex.Width));
                }
            }
        }
        _bitmaps[key] = wb;
        return wb;
    }

    private static uint Blend(uint dst, uint src)
    {
        var a = (src >> 24) & 0xFF;
        if (a == 255)
        {
            return src;
        }
        uint Mix(int shift) => (((src >> shift) & 0xFF) * a + ((dst >> shift) & 0xFF) * (255 - a)) / 255;
        return 0xFF000000u | (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
    }

    private static Texture Decode(Bitmap bmp)
    {
        var w = bmp.PixelSize.Width;
        var h = bmp.PixelSize.Height;
        var px = new uint[w * h];
        using var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = wb.Lock())
        {
            // Assets are normalized to 32-bit RGBA PNGs, which every backend can copy.
            bmp.CopyPixels(fb, AlphaFormat.Unpremul);
            var row = new int[w];
            for (var y = 0; y < h; y++)
            {
                Marshal.Copy(fb.Address + y * fb.RowBytes, row, 0, w);
                for (var x = 0; x < w; x++)
                {
                    px[y * w + x] = (uint)row[x]; // BGRA bytes in memory == 0xAARRGGBB little-endian
                }
            }
        }
        return new Texture(w, h, px);
    }
}
