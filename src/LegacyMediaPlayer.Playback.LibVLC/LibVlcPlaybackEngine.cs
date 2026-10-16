using LegacyMediaPlayer.Core;
using LibVLCSharp.Shared;
using NativeLibVlc = LibVLCSharp.Shared.LibVLC;

namespace LegacyMediaPlayer.Playback.LibVLC;

/// <summary>LibVLC 3 adapter. Commands/disposal must be serialized by the owner.</summary>
public sealed class LibVlcPlaybackEngine : IPlaybackEngine
{
    private readonly NativeLibVlc _runtime;
    private readonly MediaPlayer _player;
    private readonly object _snapshotGate = new();
    private Media? _media;
    private PlaybackSnapshot _snapshot = PlaybackSnapshot.Empty;
    private volatile bool _disposed;

    public LibVlcPlaybackEngine(string[]? options = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Milestone 0 native adapter supports Windows x64. Linux and Android need platform integration.");
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _runtime = new NativeLibVlc(options ?? ["--no-video-title-show"]);
            try { _player = new MediaPlayer(_runtime); }
            catch { _runtime.Dispose(); throw; }
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or VLCException)
        {
            throw new PlaybackException("LibVLC could not start. Restore VideoLAN.LibVLC.Windows and keep the libvlc/win-x64 folder beside the app, or start with --mock.", ex);
        }

        _player.Playing += OnPlaying;
        _player.Paused += OnPaused;
        _player.Stopped += OnStopped;
        _player.EndReached += OnEnded;
        _player.EncounteredError += OnError;
        _player.TimeChanged += OnTime;
        _player.LengthChanged += OnLength;
        _player.SeekableChanged += OnSeekable;
        _player.Volume = Snapshot.Volume;
    }

    public PlaybackCapabilities Capabilities { get; } = new(true, true, true);
    public PlaybackSnapshot Snapshot { get { lock (_snapshotGate) return _snapshot; } }
    public event EventHandler<PlaybackChangedEventArgs>? Changed;

    public void Open(MediaSource source)
    {
        CheckDisposed();
        ArgumentNullException.ThrowIfNull(source);
        if (!File.Exists(source.Path)) throw new FileNotFoundException("The media file no longer exists.", source.Path);
        // Allocate before interrupting the current source. Stop completes old callbacks
        // before assigning a new Media, so they cannot label the new source as ended.
        var next = new Media(_runtime, source.Uri);
        try
        {
            _player.Stop();
            _player.Media = next;
        }
        catch { next.Dispose(); throw; }
        _media?.Dispose();
        _media = next;
        Update(s => s with
        {
            Media = source,
            State = PlaybackState.Ready,
            Position = TimeSpan.Zero,
            Duration = TimeSpan.Zero,
            CanSeek = false,
            Error = null
        });
    }

    public void Play()
    {
        RequireMedia();
        // LibVLC 3 keeps an ended input until Stop; Play alone does not restart it.
        if (Snapshot.State is PlaybackState.Ended or PlaybackState.Failed) Stop();
        if (!_player.Play())
        {
            OnError(this, EventArgs.Empty);
            throw new PlaybackException("The media could not be played. Check that the file is readable and supported.");
        }
        // Apply preferences on the command thread, never from Playing callbacks.
        _player.Volume = Snapshot.Volume;
        _player.Mute = Snapshot.IsMuted;
    }

    public void Pause()
    {
        RequireMedia();
        if (Snapshot.State == PlaybackState.Playing) _player.SetPause(true);
    }

    public void Stop()
    {
        CheckDisposed();
        if (_media is null) return;
        _player.Stop();
        Update(s => s with { State = PlaybackState.Stopped, Position = TimeSpan.Zero, CanSeek = false });
    }

    public void Seek(TimeSpan position)
    {
        CheckDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(position, TimeSpan.Zero);
        RequireMedia();
        var snapshot = Snapshot;
        if (!snapshot.CanSeek) throw new InvalidOperationException("This media is not currently seekable.");
        var target = snapshot.Duration > TimeSpan.Zero && position > snapshot.Duration ? snapshot.Duration : position;
        _player.Time = (long)target.TotalMilliseconds;
        // Paused inputs may not emit TimeChanged until resumed. Show the requested
        // position now; subsequent native time events remain authoritative.
        Update(s => s with { Position = target });
    }

    public void SetVolume(int volume)
    {
        CheckDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 100);
        _player.Volume = volume;
        Update(s => s with { Volume = volume });
    }

    public void SetMuted(bool muted)
    {
        CheckDisposed();
        _player.Mute = muted;
        Update(s => s with { IsMuted = muted });
    }

    public IReadOnlyList<PlaybackTrack> GetTracks()
    {
        CheckDisposed();
        if (_media is null) return [];
        return _player.AudioTrackDescription.Select(t => new PlaybackTrack(t.Id, PlaybackTrackKind.Audio, t.Name))
            .Concat(_player.SpuDescription.Select(t => new PlaybackTrack(t.Id, PlaybackTrackKind.Subtitle, t.Name))).ToArray();
    }

    public void SelectTrack(PlaybackTrackKind kind, int id)
    {
        RequireMedia();
        if (!GetTracks().Any(t => t.Kind == kind && t.Id == id))
            throw new ArgumentException("The requested track is not available.", nameof(id));
        var success = kind switch
        {
            PlaybackTrackKind.Audio => _player.SetAudioTrack(id),
            PlaybackTrackKind.Subtitle => _player.SetSpu(id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (!success) throw new PlaybackException("The playback engine rejected the track selection.");
    }

    public void AttachVideoSurface(VideoSurface? surface)
    {
        CheckDisposed();
        if (surface is not null && (surface.Kind != VideoSurfaceKind.WindowsHwnd || surface.Handle == 0))
            throw new ArgumentException("A valid Windows HWND is required.", nameof(surface));
        _player.Hwnd = surface?.Handle ?? 0;
    }

    // Native callbacks only update managed data. Calling LibVLC here can deadlock.
    private void OnPlaying(object? sender, EventArgs e) => Update(s => s with { State = PlaybackState.Playing, Error = null });
    private void OnPaused(object? sender, EventArgs e) => Update(s => s with { State = PlaybackState.Paused });
    private void OnStopped(object? sender, EventArgs e) => Update(s =>
        s.State is PlaybackState.Ended or PlaybackState.Failed
            ? s : s with { State = PlaybackState.Stopped, Position = TimeSpan.Zero });
    private void OnEnded(object? sender, EventArgs e) => Update(s =>
        s.Duration == TimeSpan.Zero && s.Position == TimeSpan.Zero
            ? s with
            {
                State = PlaybackState.Failed,
                CanSeek = false,
                Error = new("no-playable-content", "The file ended without playable content. It may be empty, damaged, or unsupported.")
            }
            : s with { State = PlaybackState.Ended, Position = s.Duration > TimeSpan.Zero ? s.Duration : s.Position, CanSeek = false });
    private void OnError(object? sender, EventArgs e) => Update(s => s with
    {
        State = PlaybackState.Failed,
        CanSeek = false,
        Error = new("decode-failed", "Unable to decode this file. It may be damaged, unreadable, or unsupported.")
    });
    private void OnTime(object? sender, MediaPlayerTimeChangedEventArgs e) => Update(s => s with { Position = TimeSpan.FromMilliseconds(Math.Max(0, e.Time)) });
    private void OnLength(object? sender, MediaPlayerLengthChangedEventArgs e) => Update(s => s with { Duration = TimeSpan.FromMilliseconds(Math.Max(0, e.Length)) });
    private void OnSeekable(object? sender, MediaPlayerSeekableChangedEventArgs e) => Update(s => s with { CanSeek = e.Seekable != 0 });

    private void Update(Func<PlaybackSnapshot, PlaybackSnapshot> transform)
    {
        PlaybackSnapshot next;
        lock (_snapshotGate)
        {
            if (_disposed) return;
            next = transform(_snapshot);
            _snapshot = next;
        }
        Changed?.Invoke(this, new(next));
    }

    private void RequireMedia()
    {
        CheckDisposed();
        if (_media is null) throw new InvalidOperationException("Open a local media file first.");
    }

    private void CheckDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.Playing -= OnPlaying;
        _player.Paused -= OnPaused;
        _player.Stopped -= OnStopped;
        _player.EndReached -= OnEnded;
        _player.EncounteredError -= OnError;
        _player.TimeChanged -= OnTime;
        _player.LengthChanged -= OnLength;
        _player.SeekableChanged -= OnSeekable;
        Changed = null;
        _player.Stop();
        _player.Hwnd = 0;
        _player.Dispose();
        _media?.Dispose();
        _runtime.Dispose();
    }
}
