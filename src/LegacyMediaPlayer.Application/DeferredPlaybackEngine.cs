using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

/// <summary>
/// Creates a backend once on a worker, only when playback is requested.
/// Commands/disposal retain IPlaybackEngine's serialized-caller contract.
/// Reading state, changing audio preferences and attaching a surface never load codecs.
/// </summary>
public sealed class DeferredPlaybackEngine : IPlaybackEngine
{
    private readonly Func<IPlaybackEngine> _factory;
    private readonly object _gate = new();
    private IPlaybackEngine? _backend;
    private Task? _initialization;
    private PlaybackSnapshot _snapshot = PlaybackSnapshot.Empty;
    private VideoSurface? _surface;
    private bool _disposed;

    public DeferredPlaybackEngine(Func<IPlaybackEngine> factory, PlaybackCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(capabilities);
        _factory = factory;
        Capabilities = capabilities;
    }

    public PlaybackCapabilities Capabilities { get; }
    public PlaybackSnapshot Snapshot { get { lock (_gate) return _snapshot; } }
    public event EventHandler<PlaybackChangedEventArgs>? Changed;

    public Task InitializeAsync()
    {
        lock (_gate)
        {
            CheckDisposed();
            return _initialization ??= Task.Run(InitializeCore);
        }
    }

    private void InitializeCore()
    {
        IPlaybackEngine? created = null;
        try
        {
            // Never hold the gate during DLL/plugin discovery: closing must stay responsive.
            created = _factory();
            lock (_gate)
            {
                CheckDisposed();
                // No media is open and no events are subscribed yet. Apply preferences
                // atomically with publication so edits made during startup are retained.
                created.SetVolume(_snapshot.Volume);
                created.SetMuted(_snapshot.IsMuted);
                if (_surface is not null) created.AttachVideoSurface(_surface);
                created.Changed += OnChanged;
                _snapshot = created.Snapshot;
                _backend = created;
                created = null; // Ownership transferred; Dispose handles the published engine.
            }
        }
        catch (Exception ex) when (ex is not PlaybackException and not ObjectDisposedException)
        {
            throw new PlaybackException("The playback engine could not initialize. Check the bundled LibVLC folder and native dependencies.", ex);
        }
        finally { created?.Dispose(); }
    }

    private IPlaybackEngine Backend()
    {
        InitializeAsync().GetAwaiter().GetResult();
        lock (_gate) { CheckDisposed(); return _backend!; }
    }

    public void Open(MediaSource source) => Backend().Open(source);
    public void Play() => Backend().Play();
    public void Pause() { Current()?.Pause(); }
    public void Stop() { Current()?.Stop(); }
    public void Seek(TimeSpan position) => Backend().Seek(position);
    public IReadOnlyList<PlaybackTrack> GetTracks() => Current()?.GetTracks() ?? [];
    public void SelectTrack(PlaybackTrackKind kind, int id) => Backend().SelectTrack(kind, id);

    public void SetVolume(int volume)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 100);
        IPlaybackEngine? backend;
        PlaybackSnapshot snapshot;
        lock (_gate)
        {
            CheckDisposed();
            backend = _backend;
            snapshot = _snapshot = _snapshot with { Volume = volume };
        }
        if (backend is not null) backend.SetVolume(volume);
        else Changed?.Invoke(this, new(snapshot));
    }

    public void SetMuted(bool muted)
    {
        IPlaybackEngine? backend;
        PlaybackSnapshot snapshot;
        lock (_gate)
        {
            CheckDisposed();
            backend = _backend;
            snapshot = _snapshot = _snapshot with { IsMuted = muted };
        }
        if (backend is not null) backend.SetMuted(muted);
        else Changed?.Invoke(this, new(snapshot));
    }

    public void AttachVideoSurface(VideoSurface? surface)
    {
        IPlaybackEngine? backend;
        lock (_gate) { CheckDisposed(); _surface = surface; backend = _backend; }
        backend?.AttachVideoSurface(surface);
    }

    private IPlaybackEngine? Current()
    {
        lock (_gate) { CheckDisposed(); return _backend; }
    }

    private void OnChanged(object? sender, PlaybackChangedEventArgs e)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _snapshot = _backend?.Snapshot ?? e.Snapshot;
        }
        Changed?.Invoke(this, e);
    }

    private void CheckDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        IPlaybackEngine? backend;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            backend = _backend;
            _backend = null;
            Changed = null;
        }
        // A factory still running will observe disposal and release its result on its worker.
        if (backend is not null) { backend.Changed -= OnChanged; backend.Dispose(); }
    }
}
