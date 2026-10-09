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

    /// <summary>Starts a looping ambient sound under the music, or stops it when <paramref name="loop"/> is null.</summary>
    /// <param name="loop">Loop key (file name under Assets/Audio/Ambience without extension).</param>
    void PlayAmbience(string? loop);

    /// <summary>Plays a one-shot sound effect.</summary>
    /// <param name="sfx">Effect key (file name under Assets/Audio/Sfx without extension).</param>
    void PlaySfx(string sfx);

    /// <summary>Sets volumes (0-100).</summary>
    /// <param name="music">Music volume.</param>
    /// <param name="sfx">Effects volume.</param>
    /// <param name="ambience">Ambient sound volume.</param>
    void SetVolumes(int music, int sfx, int ambience);
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
    public void PlayAmbience(string? loop) { }
    /// <inheritdoc />
    public void PlaySfx(string sfx) { }
    /// <inheritdoc />
    public void SetVolumes(int music, int sfx, int ambience) { }
    /// <inheritdoc />
    public void Dispose() { }
}

/// <summary>
/// Cross-platform audio using OpenAL Soft (via Silk.NET, native libraries bundled for every RID)
/// and NVorbis for decoding Ogg Vorbis assets. Music and ambience each stream on a background thread.
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
    private readonly StreamChannel _music;
    private readonly StreamChannel _ambience;
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
        _music = new StreamChannel(this, "Music", 0.6f);
        _ambience = new StreamChannel(this, "Ambience", 0.5f);
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

    /// <summary>Opens a bundled Ogg file and decodes its first samples (used by <c>--smoke-test</c>).</summary>
    /// <param name="path">Path under Assets/Audio, e.g. <c>Music/boss.ogg</c>.</param>
    /// <exception cref="InvalidDataException">Thrown when the file decodes to nothing.</exception>
    public static void CheckDecodes(string path)
    {
        using var reader = new VorbisReader(OpenAsset(path), closeOnDispose: true);
        var buf = new float[4096 * reader.Channels];
        if (reader.ReadSamples(buf, 0, buf.Length) <= 0)
        {
            throw new InvalidDataException(path + " contains no audio");
        }
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
        if (!_disposed)
        {
            _music.Play(track);
        }
    }

    /// <inheritdoc />
    public void StopMusic() => _music.Stop();

    /// <inheritdoc />
    public void PlayAmbience(string? loop)
    {
        if (_disposed)
        {
            return;
        }
        if (string.IsNullOrEmpty(loop))
        {
            _ambience.Stop();
        }
        else
        {
            _ambience.Play(loop);
        }
    }

    /// <inheritdoc />
    public void SetVolumes(int music, int sfx, int ambience)
    {
        _sfxGain = Math.Clamp(sfx, 0, 100) / 100f;
        _music.SetGain(Math.Clamp(music, 0, 100) / 100f);
        _ambience.SetGain(Math.Clamp(ambience, 0, 100) / 100f);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _music.Stop();
        _ambience.Stop();
        lock (_lock)
        {
            _disposed = true;
            fixed (uint* v = _voices)
            {
                _al.DeleteSources(SfxVoices, v);
            }
            _music.Delete();
            _ambience.Delete();
            foreach (var b in _sfxBuffers.Values)
            {
                _al.DeleteBuffer(b);
            }
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
            _alc.CloseDevice(_device);
        }
    }

    /// <summary>One looping Ogg stream (music or ambience) with its own source, buffers and thread.</summary>
    private sealed class StreamChannel
    {
        private readonly OpenAlAudioService _owner;
        private readonly string _folder;
        private readonly uint _source;
        private readonly uint[] _buffers = new uint[StreamBuffers];
        private Thread? _thread;
        private volatile bool _stop;
        private string? _current;
        private float _gain;

        public StreamChannel(OpenAlAudioService owner, string folder, float gain)
        {
            _owner = owner;
            _folder = folder;
            _gain = gain;
            _source = owner._al.GenSource();
            fixed (uint* b = _buffers)
            {
                owner._al.GenBuffers(StreamBuffers, b);
            }
        }

        private AL Al => _owner._al;

        public void Play(string key)
        {
            if (key == _current)
            {
                return;
            }
            Stop();
            _current = key;
            _stop = false;
            _thread = new Thread(() => Loop(key)) { IsBackground = true, Name = "AVAMMB1 " + _folder.ToLowerInvariant() };
            _thread.Start();
        }

        public void SetGain(float gain)
        {
            _gain = gain;
            lock (_owner._lock)
            {
                if (!_owner._disposed)
                {
                    Al.SetSourceProperty(_source, SourceFloat.Gain, _gain);
                }
            }
        }

        public void Stop()
        {
            _stop = true;
            _thread?.Join(500);
            _thread = null;
            _current = null;
            lock (_owner._lock)
            {
                if (_owner._disposed)
                {
                    return;
                }
                Al.SourceStop(_source);
                Al.GetSourceProperty(_source, GetSourceInteger.BuffersQueued, out var queued);
                while (queued-- > 0)
                {
                    uint b;
                    Al.SourceUnqueueBuffers(_source, 1, &b);
                }
            }
        }

        /// <summary>Releases the OpenAL objects (caller holds the lock).</summary>
        public void Delete()
        {
            Al.DeleteSource(_source);
            fixed (uint* b = _buffers)
            {
                Al.DeleteBuffers(StreamBuffers, b);
            }
        }

        private void Loop(string key)
        {
            VorbisReader? reader = null;
            var gate = _owner._lock;
            try
            {
                var stream = OpenAsset($"{_folder}/{key}.ogg");
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
                    lock (gate)
                    {
                        fixed (short* p = pcm)
                        {
                            Al.BufferData(buffer, format, p, n * 2, reader.SampleRate);
                        }
                        var b = buffer;
                        Al.SourceQueueBuffers(_source, 1, &b);
                    }
                    return true;
                }

                lock (gate)
                {
                    Al.SetSourceProperty(_source, SourceFloat.Gain, _gain);
                }
                foreach (var b in _buffers)
                {
                    Fill(b);
                }
                lock (gate)
                {
                    Al.SourcePlay(_source);
                }
                while (!_stop)
                {
                    int processed;
                    lock (gate)
                    {
                        Al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out processed);
                    }
                    while (processed-- > 0 && !_stop)
                    {
                        uint b;
                        lock (gate)
                        {
                            Al.SourceUnqueueBuffers(_source, 1, &b);
                        }
                        Fill(b);
                    }
                    lock (gate)
                    {
                        Al.GetSourceProperty(_source, GetSourceInteger.SourceState, out var state);
                        if (state != (int)SourceState.Playing && !_stop)
                        {
                            Al.SourcePlay(_source); // recover from underrun
                        }
                    }
                    Thread.Sleep(30);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FileNotFoundException)
            {
                // Missing or unreadable file: play silence.
            }
            finally
            {
                reader?.Dispose();
            }
        }
    }
}
