using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AVAMMB1.App.Rendering;

/// <summary>Small pictures of the 3D view kept with saved games.</summary>
public static class SaveThumbnail
{
    /// <summary>Thumbnail width in pixels.</summary>
    public const int Width = 160;

    /// <summary>Thumbnail height in pixels.</summary>
    public const int Height = 120;

    /// <summary>Renders the scene at the classic resolution and shrinks it to a PNG thumbnail.</summary>
    /// <param name="scene">What the party sees, or null.</param>
    /// <returns>PNG bytes, or null when there is no scene.</returns>
    public static byte[]? Render(SceneDescription? scene)
    {
        if (scene is null)
        {
            return null;
        }
        var r = new SceneRenderer(App.Textures, SceneRenderer.ClassicHeight);
        r.Render(scene, Camera.At(scene.X, scene.Y, scene.Facing));
        using var bmp = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bmp.Lock())
        {
            unsafe
            {
                for (var y = 0; y < Height; y++)
                {
                    var dst = new Span<uint>((void*)(fb.Address + y * fb.RowBytes), Width);
                    var row = y * r.Height / Height * r.Width;
                    for (var x = 0; x < Width; x++)
                    {
                        dst[x] = r.Pixels[row + x * r.Width / Width];
                    }
                }
            }
        }
        using var png = new MemoryStream();
        bmp.Save(png);
        return png.ToArray();
    }
}
