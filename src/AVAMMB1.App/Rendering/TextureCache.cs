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
}

/// <summary>Loads and caches images from the embedded <c>Assets/Graphics</c> folder.</summary>
public sealed class TextureCache
{
    private readonly Dictionary<string, Texture> _textures = new();
    private readonly Dictionary<string, Bitmap?> _bitmaps = new();

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
