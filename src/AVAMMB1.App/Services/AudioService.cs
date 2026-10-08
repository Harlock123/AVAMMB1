using Avalonia.Platform;
using System.Runtime.InteropServices;
using NVorbis;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenAL;

namespace AVAMMB1.App.Services;

/// <summary>Plays music and sound effects.</summary>
public interface IAudioService : IDisposable
{
    /// <summary>Whether audio output is actually available.</summary>
    bool IsAvailable { get; }

    /// <summary>Short status text for diagnostics.</summary>
    string Status { get; }

    /// <summary>Starts a looping music track (no-op if it is already playing).</summary>
    /// <param name="track">Track key (file name under Assets/Audio/Music without extension).</param>
    void PlayMusic(string track);

    /// <summary>Stops music.</summary>
    void StopMusic();

    /// <summary>Plays a one-shot sound effect.</summary>
    /// <param name="sfx">Effect key (file name under Assets/Audio/Sfx without extension).</param>
    void PlaySfx(string sfx);

    /// <summary>Sets volumes (0-100).</summary>
    /// <param name="music">Music volume.</param>
    /// <param name="sfx">Effects volume.</param>
    void SetVolumes(int music, int sfx);
}

/// <summary>Silent implementation used when no audio device exists (CI, headless screenshots).</summary>
public sealed class NullAudioService(string reason) : IAudioService
{
    /// <inheritdoc />
    public bool IsAvailable => false;
    /// <inheritdoc />
    public string Status => "Audio disabled: " + reason;
    /// <inheritdoc />
    public void PlayMusic(string track) { }
    /// <inheritdoc />
    public void StopMusic() { }
    /// <inheritdoc />
    public void PlaySfx(string sfx) { }
    /// <inheritdoc />
    public void SetVolumes(int music, int sfx) { }
    /// <inheritdoc />
    public void Dispose() { }
}

/// <summary>
/// Cross-platform audio using OpenAL Soft (via Silk.NET, native libraries bundled for every RID)
/// and NVorbis for decoding Ogg Vorbis assets. Music streams on a background thread.
/// </summary>
public sealed unsafe class OpenAlAudioService : IAudioService
{
    private const int SfxVoices = 10;
    private const int StreamBuffers = 4;
    private const int StreamChunkFrames = 8192;

    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly Dictionary<string, uint> _sfxBuffers = new();
    private readonly uint[] _voices = new uint[SfxVoices];
    private readonly object _lock = new();
    private uint _musicSource;
    private readonly uint[] _musicBuffers = new uint[StreamBuffers];
    private Thread? _musicThread;
    private volatile bool _musicStop;
    private string? _currentTrack;
    private float _musicGain = 0.6f;
    private float _sfxGain = 0.8f;
    private int _nextVoice;
    private bool _disposed;

    private OpenAlAudioService(ALContext alc, AL al, Device* device, Context* context)
    {
        _alc = alc;
        _al = al;
        _device = device;
        _context = context;
        fixed (uint* v = _voices)
        {
            _al.GenSources(SfxVoices, v);
        }
        _musicSource = _al.GenSource();
        fixed (uint* b = _musicBuffers)
        {
            _al.GenBuffers(StreamBuffers, b);
        }
    }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public string Status => LibraryDetail;

    /// <summary>Tries to open the default audio device, falling back to <see cref="NullAudioService"/>.</summary>
    public static IAudioService Create()
    {
        try
        {
            if (!TryLoadLibrary(out var handle, out var detail))
            {
                return new NullAudioService(detail);
            }
            var native = new LamdaNativeContext(name => NativeLibrary.TryGetExport(handle, name, out var p) ? p : 0);
            var alc = new ALContext(native);
            var al = new AL(native);
            var device = alc.OpenDevice("");
            if (device == null)
            {
                return new NullAudioService("no audio device");
            }
            var context = alc.CreateContext(device, null);
            alc.MakeContextCurrent(context);
            al.GetError();
            return new OpenAlAudioService(alc, al, device, context) { LibraryDetail = detail };
        }
        catch (Exception ex)
        {
            return new NullAudioService(ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>Which OpenAL library was loaded (for diagnostics).</summary>
    public string LibraryDetail { get; private init; } = "";

    /// <summary>
    /// Loads the OpenAL Soft library bundled with the game. .NET's own native-library probing is used
    /// because it knows where single-file builds extract their native libraries; a system-wide OpenAL
    /// is only used as a fallback.
    /// </summary>
    /// <param name="handle">Library handle.</param>
    /// <param name="detail">Which library was loaded, or why none was.</param>
    public static bool TryLoadLibrary(out nint handle, out string detail)
    {
        string[] bundled = OperatingSystem.IsWindows() ? ["soft_oal.dll"]
            : OperatingSystem.IsMacOS() ? ["libopenal.dylib"]
            : ["libopenal.so"];
        string[] system = OperatingSystem.IsWindows() ? ["OpenAL32.dll"]
            : OperatingSystem.IsMacOS() ? ["/System/Library/Frameworks/OpenAL.framework/OpenAL"]
            : ["libopenal.so.1"];
        foreach (var name in bundled)
        {
            if (NativeLibrary.TryLoad(name, typeof(OpenAlAudioService).Assembly, DllImportSearchPath.AssemblyDirectory, out handle))
            {
                detail = "bundled OpenAL Soft (" + name + ")";
                return true;
            }
        }
        foreach (var name in system)
        {
            if (NativeLibrary.TryLoad(name, out handle))
            {
                detail = "system OpenAL (" + name + ")";
                return true;
            }
        }
        handle = 0;
        detail = "OpenAL library not found";
        return false;
    }

    private static Stream OpenAsset(string path) => AssetLoader.Open(new Uri($"avares://AVAMMB1/Assets/Audio/{path}"));

    private static (short[] Pcm, int Channels, int Rate) DecodeAll(string path)
    {
        using var stream = OpenAsset(path);
        using var reader = new VorbisReader(stream, closeOnDispose: false);
        var floats = new List<float>((int)Math.Min(reader.TotalSamples * reader.Channels, 4_000_000));
        var buf = new float[4096 * reader.Channels];
        int n;
        while ((n = reader.ReadSamples(buf, 0, buf.Length)) > 0)
        {
            for (var i = 0; i < n; i++)
            {
                floats.Add(buf[i]);
            }
        }
        var pcm = new short[floats.Count];
        for (var i = 0; i < pcm.Length; i++)
        {
            pcm[i] = (short)Math.Clamp(floats[i] * 32767f, short.MinValue, short.MaxValue);
        }
        return (pcm, reader.Channels, reader.SampleRate);
    }

    private uint GetSfxBuffer(string key)
    {
        if (_sfxBuffers.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var (pcm, channels, rate) = DecodeAll($"Sfx/{key}.ogg");
        var buffer = _al.GenBuffer();
        fixed (short* p = pcm)
        {
            _al.BufferData(buffer, channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, p, pcm.Length * 2, rate);
        }
        _sfxBuffers[key] = buffer;
        return buffer;
    }

    /// <summary>Decodes and uploads a sound without playing it (used by <c>--smoke-test</c>).</summary>
    /// <param name="sfx">Effect key.</param>
    /// <returns>OpenAL buffer id.</returns>
    public uint Preload(string sfx)
    {
        lock (_lock)
        {
            return GetSfxBuffer(sfx);
        }
    }

    /// <inheritdoc />
    public void PlaySfx(string sfx)
    {
        if (_disposed || _sfxGain <= 0)
        {
            return;
        }
        lock (_lock)
        {
            try
            {
                var buffer = GetSfxBuffer(sfx);
                var voice = _voices[_nextVoice];
                _nextVoice = (_nextVoice + 1) % SfxVoices;
                _al.SourceStop(voice);
                _al.SetSourceProperty(voice, SourceInteger.Buffer, (int)buffer);
                _al.SetSourceProperty(voice, SourceFloat.Gain, _sfxGain);
                _al.SourcePlay(voice);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FileNotFoundException)
            {
                // Missing or broken effect: ignore rather than crash the game.
            }
        }
    }

    /// <inheritdoc />
    public void PlayMusic(string track)
    {
        if (_disposed || track == _currentTrack)
        {
            return;
        }
        StopMusic();
        _currentTrack = track;
        _musicStop = false;
        _musicThread = new Thread(() => StreamLoop(track)) { IsBackground = true, Name = "AVAMMB1 music" };
        _musicThread.Start();
    }

    private void StreamLoop(string track)
    {
        VorbisReader? reader = null;
        try
        {
            var stream = OpenAsset($"Music/{track}.ogg");
            reader = new VorbisReader(stream, closeOnDispose: true);
            var format = reader.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
            var floats = new float[StreamChunkFrames * reader.Channels];
            var pcm = new short[floats.Length];

            bool Fill(uint buffer)
            {
                var n = reader.ReadSamples(floats, 0, floats.Length);
                if (n <= 0)
                {
                    reader.SamplePosition = 0; // loop
                    n = reader.ReadSamples(floats, 0, floats.Length);
                    if (n <= 0)
                    {
                        return false;
                    }
                }
                for (var i = 0; i < n; i++)
                {
                    pcm[i] = (short)Math.Clamp(floats[i] * 32767f, short.MinValue, short.MaxValue);
                }
                lock (_lock)
                {
                    fixed (short* p = pcm)
                    {
                        _al.BufferData(buffer, format, p, n * 2, reader.SampleRate);
                    }
                    var b = buffer;
                    _al.SourceQueueBuffers(_musicSource, 1, &b);
                }
                return true;
            }

            lock (_lock)
            {
                _al.SetSourceProperty(_musicSource, SourceFloat.Gain, _musicGain);
            }
            foreach (var b in _musicBuffers)
            {
                Fill(b);
            }
            lock (_lock)
            {
                _al.SourcePlay(_musicSource);
            }
            while (!_musicStop)
            {
                int processed;
                lock (_lock)
                {
                    _al.GetSourceProperty(_musicSource, GetSourceInteger.BuffersProcessed, out processed);
                }
                while (processed-- > 0 && !_musicStop)
                {
                    uint b;
                    lock (_lock)
                    {
                        _al.SourceUnqueueBuffers(_musicSource, 1, &b);
                    }
                    Fill(b);
                }
                lock (_lock)
                {
                    _al.GetSourceProperty(_musicSource, GetSourceInteger.SourceState, out var state);
                    if (state != (int)SourceState.Playing && !_musicStop)
                    {
                        _al.SourcePlay(_musicSource); // recover from underrun
                    }
                }
                Thread.Sleep(30);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FileNotFoundException)
        {
            // Missing or unreadable track: play silence.
        }
        finally
        {
            reader?.Dispose();
        }
    }

    /// <inheritdoc />
    public void StopMusic()
    {
        _musicStop = true;
        _musicThread?.Join(500);
        _musicThread = null;
        _currentTrack = null;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _al.SourceStop(_musicSource);
            _al.GetSourceProperty(_musicSource, GetSourceInteger.BuffersQueued, out var queued);
            while (queued-- > 0)
            {
                uint b;
                _al.SourceUnqueueBuffers(_musicSource, 1, &b);
            }
        }
    }

    /// <inheritdoc />
    public void SetVolumes(int music, int sfx)
    {
        _musicGain = Math.Clamp(music, 0, 100) / 100f;
        _sfxGain = Math.Clamp(sfx, 0, 100) / 100f;
        lock (_lock)
        {
            if (!_disposed)
            {
                _al.SetSourceProperty(_musicSource, SourceFloat.Gain, _musicGain);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        StopMusic();
        lock (_lock)
        {
            _disposed = true;
            fixed (uint* v = _voices)
            {
                _al.DeleteSources(SfxVoices, v);
            }
            _al.DeleteSource(_musicSource);
            fixed (uint* b = _musicBuffers)
            {
                _al.DeleteBuffers(StreamBuffers, b);
            }
            foreach (var b in _sfxBuffers.Values)
            {
                _al.DeleteBuffer(b);
            }
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
            _alc.CloseDevice(_device);
        }
    }
}
