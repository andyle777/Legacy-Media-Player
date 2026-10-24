using LegacyMediaPlayer.Core;
using LibVLCSharp.Shared;
using NativeLibVlc = LibVLCSharp.Shared.LibVLC;

namespace LegacyMediaPlayer.Playback.LibVLC;

/// <summary>LibVLC 3 adapter. Commands/disposal must be serialized by the owner.</summary>
public sealed partial class LibVlcPlaybackEngine : IPlaybackEngine
{
    private readonly Action<string>? _trace;
    private readonly NativeLibVlc _runtime;
    private readonly MediaPlayer _player;
    private readonly object _snapshotGate = new();
    private Media? _media;
    private PlaybackSnapshot _snapshot = PlaybackSnapshot.Empty;
    private volatile bool _disposed;
    private volatile bool _acceptNativeEvents = true;
    private bool _hadVideoOutput; // Accessed under _snapshotGate.
    private bool _hadClock;
    private bool _playRequested; // Serialized command thread; native Playing arrives asynchronously.

    public LibVlcPlaybackEngine(string[]? options = null, Action<string>? trace = null, bool enableHardwareDecoding = true)
    {
        _trace = trace;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Milestone 0 native adapter supports Windows x64. Linux and Android need platform integration.");
        try
        {
            var runtimeOptions = options ?? ["--no-video-title-show"];
            // Explicit plugin/cache options (including the packaging reset) retain
            // their caller's behavior. Normal packaged playback uses verified data.
            if (!runtimeOptions.Any(o => o.Contains("plugins-", StringComparison.Ordinal)))
            {
                trace?.Invoke("libvlc.cache.verify.begin");
                var packaged = PackagedPluginCache.Validate(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));
                trace?.Invoke("libvlc.cache.verify.end");
                trace?.Invoke(packaged ? "libvlc.plugins.cache-only" : "libvlc.plugins.discovery");
                if (packaged) runtimeOptions = [.. runtimeOptions, "--plugins-cache", "--no-plugins-scan"];
            }
            trace?.Invoke("libvlc.load.begin");
            LibVLCSharp.Shared.Core.Initialize();
            trace?.Invoke("libvlc.load.end");
            trace?.Invoke("libvlc.runtime.begin");
            _runtime = new NativeLibVlc(runtimeOptions);
            trace?.Invoke("libvlc.runtime.end");
            trace?.Invoke("libvlc.player.begin");
            try
            {
                _player = new MediaPlayer(_runtime)
                {
                    EnableHardwareDecoding = enableHardwareDecoding,
                    EnableKeyInput = false,
                    EnableMouseInput = false
                };
            }
            catch { _runtime.Dispose(); throw; }
            trace?.Invoke("libvlc.player.end");
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or VLCException)
        {
            throw new PlaybackException("LibVLC could not start. Restore VideoLAN.LibVLC.Windows and keep the libvlc/win-x64 folder beside the app, or start with --mock.", ex);
        }

        _runtime.Log += OnNativeLog;
        _player.Playing += OnPlaying;
        _player.Paused += OnPaused;
        _player.Stopped += OnStopped;
        _player.EndReached += OnEnded;
        _player.EncounteredError += OnError;
        _player.TimeChanged += OnTime;
        _player.LengthChanged += OnLength;
        _player.SeekableChanged += OnSeekable;
        _player.ESAdded += OnElementaryStreamAdded;
        _player.ESDeleted += OnSubtitleStreamDeleted;
        _player.ESSelected += OnSubtitleStreamSelected;
        _player.Vout += OnVideoOutput;
        _player.Volume = Snapshot.Volume;
    }

    public PlaybackCapabilities Capabilities { get; } = new(true, true, true);
    public PlaybackSnapshot Snapshot { get { lock (_snapshotGate) return _snapshot; } }
    public event EventHandler<PlaybackChangedEventArgs>? Changed;

    public void Open(MediaSource source)
    {
        _trace?.Invoke("playback.media.open.begin");
        CheckDisposed();
        ArgumentNullException.ThrowIfNull(source);
        if (!File.Exists(source.Path)) throw new FileNotFoundException("The media file no longer exists.", source.Path);
        // Allocate before interrupting the current source. Stop completes old callbacks
        // before assigning a new Media, so they cannot label the new source as ended.
        _trace?.Invoke("playback.media.create.begin");
        var next = new Media(_runtime, source.Uri);
        _trace?.Invoke("playback.media.create.end");
        _playRequested = false;
        _acceptNativeEvents = false;
        try
        {
            _trace?.Invoke("playback.media.stop.begin");
            _player.Stop();
            _trace?.Invoke("playback.media.stop.end");
            _trace?.Invoke("playback.media.attach.begin");
            _player.Media = next;
            _trace?.Invoke("playback.media.attach.end");
        }
        catch { next.Dispose(); _acceptNativeEvents = true; throw; }
        _media?.Dispose();
        _media = next;
        ResetSubtitles();
        Update(s =>
        {
            _hadVideoOutput = false;
            _hadClock = false;
            return s with
            {
                Media = source,
                State = PlaybackState.Ready,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero,
                CanSeek = false,
                Error = null,
                HasVideo = false,
                HasVideoOutput = false,
                AcceptedRate = null,
                RateError = null
            };
        });
        _acceptNativeEvents = true;
        _trace?.Invoke("playback.media.open.end");
    }

    public void Play()
    {
        _trace?.Invoke("playback.play.begin");
        RequireMedia();
        // LibVLC 3 keeps an ended input until Stop; Play alone does not restart it.
        if (Snapshot.State is PlaybackState.Ended or PlaybackState.Failed) Stop();
        // An explicit Stop retains the last error for display; a new attempt clears it.
        if (Snapshot.State == PlaybackState.Stopped)
            Update(s => s with { State = PlaybackState.Ready, Error = null });
        _playRequested = true;
        _subtitleClock = 0;
        if (_subtitleRequest is not null) Update(s => s with { SubtitlePending = true, SubtitleRefreshRequired = true });
        if (!_player.Play())
        {
            _playRequested = false;
            OnError(this, EventArgs.Empty);
            throw new PlaybackException("The media could not be played. Check that the file is readable and supported.");
        }
        // Apply preferences on the command thread, never from Playing callbacks.
        _player.Volume = Snapshot.Volume;
        _player.Mute = Snapshot.IsMuted;
        ApplyRate(Snapshot.Rate);
        _trace?.Invoke("playback.play.end");
    }

    public void Pause()
    {
        RequireMedia();
        _subtitleClock = 0;
        if (Snapshot.State == PlaybackState.Playing) _player.SetPause(true);
    }

    public void Stop()
    {
        CheckDisposed();
        _playRequested = false;
        if (_media is null) return;
        _acceptNativeEvents = false;
        try
        {
            _player.Stop();
            _subtitleClock = 0;
            _subtitleApplied = false;
            _subtitleRequest = _subtitleRequest?.ForReplay();
            _subtitleTarget = _subtitleRequest?.TrackId;
            Update(s =>
            {
                _hadVideoOutput = false;
                return s with { State = PlaybackState.Stopped, Position = TimeSpan.Zero, CanSeek = false, HasVideoOutput = false, AcceptedRate = null, RateError = null,
                    SubtitleTracks = Array.Empty<PlaybackTrack>(), SelectedSubtitleId = -1, SubtitlePending = false, SubtitleRefreshRequired = false };
            });
        }
        finally { _acceptNativeEvents = true; }
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

    public void SetRate(float rate)
    {
        CheckDisposed(); PlaybackRates.Validate(rate);
        var state = Snapshot.State;
        if (state is PlaybackState.Playing or PlaybackState.Paused || state == PlaybackState.Ready && _playRequested) ApplyRate(rate);
        else Update(s => s with { Rate = rate, AcceptedRate = null, RateError = null });
    }

    private void ApplyRate(float rate)
    {
        // Commands run on the serialized owner thread. Never call native transport
        // from Playing/ES/time callbacks or hold _snapshotGate across native calls.
        var result = NativePlaybackRate.Apply(Snapshot, rate, _player.SetRate, () => _player.Rate);
        Update(s => s with { Rate = result.Rate, AcceptedRate = result.AcceptedRate, RateError = result.RateError });
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
        if (kind == PlaybackTrackKind.Subtitle)
        {
            var track = Snapshot.SubtitleTracks.FirstOrDefault(t => t.Id == id);
            if (id != -1 && track is null) { SubtitleFailure("The requested subtitle track is no longer available."); return; }
            SetSubtitles(id == -1 ? SubtitleSelection.Off : SubtitleSelection.Track(id, track!.Name));
            return;
        }
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
    private void OnPlaying(object? sender, EventArgs e)
    {
        _trace?.Invoke("playback.playing");
        UpdateNative(s => s with { State = PlaybackState.Playing, SubtitleRefreshRequired = true });
    }
    private void OnPaused(object? sender, EventArgs e) => UpdateNative(s => s with { State = PlaybackState.Paused });
    private void OnStopped(object? sender, EventArgs e) => UpdateNative(s =>
        s.State is PlaybackState.Ended or PlaybackState.Failed
            ? s : s with { State = PlaybackState.Stopped, Position = TimeSpan.Zero, HasVideoOutput = false });
    private void OnEnded(object? sender, EventArgs e) => UpdateNative(s =>
        s.HasVideo && !_hadVideoOutput
            ? s with
            {
                State = PlaybackState.Failed, CanSeek = false, HasVideoOutput = false,
                Error = new("video-unavailable", "The video stream produced no output. It may be damaged or use an unsupported codec.")
            }
            : s.Duration == TimeSpan.Zero && s.Position == TimeSpan.Zero && !_hadVideoOutput
            ? s with
            {
                State = PlaybackState.Failed,
                CanSeek = false,
                HasVideoOutput = false,
                Error = new("no-playable-content", "The file ended without playable content. It may be empty, damaged, or unsupported.")
            }
            : s with { State = PlaybackState.Ended, Position = s.Duration > TimeSpan.Zero ? s.Duration : s.Position, CanSeek = false, HasVideoOutput = false });
    private void OnError(object? sender, EventArgs e) => UpdateNative(s => s with
    {
        State = PlaybackState.Failed,
        CanSeek = false,
        HasVideoOutput = false,
        Error = new("decode-failed", "Unable to decode this file. It may be damaged, unreadable, or unsupported.")
    });
    private void OnTime(object? sender, MediaPlayerTimeChangedEventArgs e) => UpdateNative(s =>
    {
        if (!_hadClock && e.Time > 0) { _hadClock = true; _trace?.Invoke("playback.clock.first"); }
        return s with { Position = TimeSpan.FromMilliseconds(Math.Max(0, e.Time)), SubtitleRefreshRequired = s.SubtitleRefreshRequired || s.SubtitlePending };
    });
    private void OnLength(object? sender, MediaPlayerLengthChangedEventArgs e) => UpdateNative(s => s with { Duration = TimeSpan.FromMilliseconds(Math.Max(0, e.Length)) });
    private void OnSeekable(object? sender, MediaPlayerSeekableChangedEventArgs e) => UpdateNative(s => s with { CanSeek = e.Seekable != 0 });

    private void OnElementaryStreamAdded(object? sender, MediaPlayerESAddedEventArgs e)
    {
        if (e.Type == TrackType.Audio) _trace?.Invoke("playback.stream.audio");
        if (e.Type == TrackType.Video) _trace?.Invoke("playback.stream.video");
        if (e.Type == TrackType.Video) UpdateNative(s => s with { HasVideo = true });
        if (e.Type is TrackType.Text or TrackType.Video) RequestSubtitleRefresh();
    }
    private void OnVideoOutput(object? sender, MediaPlayerVoutEventArgs e)
    {
        UpdateNative(s =>
        {
            if (s.State is PlaybackState.Stopped or PlaybackState.Ended or PlaybackState.Failed)
                return s with { HasVideoOutput = false };
            if (!_hadVideoOutput && e.Count > 0) _trace?.Invoke("playback.video-output.first");
            _hadVideoOutput |= e.Count > 0;
            return s with
            {
                HasVideo = s.HasVideo || e.Count > 0, HasVideoOutput = e.Count > 0,
                // A failed output attempt may be followed by a successful format fallback.
                Error = e.Count > 0 && s.Error?.Code == "video-output-failed" ? null : s.Error
            };
        });
    }

    private void OnNativeLog(object? sender, LogEventArgs e)
    {
        // LibVLC can keep playing audio when creating the video output fails,
        // without raising EncounteredError. This pinned-runtime error is otherwise silent.
        if (e.Level != LogLevel.Error) return;
        if (e.Message.Contains("Codec `", StringComparison.Ordinal) &&
            e.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
            UpdateNative(s => s with
            {
                // The log does not identify the stream category. Optional subtitle
                // or audio failures must not disable otherwise playable video.
                Error = new("unsupported-track", "An audio, video or subtitle track uses an unsupported codec. Other tracks may still play.")
            });
        else if (e.Message.Contains("video output creation failed", StringComparison.OrdinalIgnoreCase) ||
                 e.Message.Contains("failed to create video output", StringComparison.OrdinalIgnoreCase))
            UpdateNative(s => s with
            {
                HasVideoOutput = false,
                Error = new("video-output-failed", "Video could not be rendered. Stop playback, check your graphics driver and the bundled LibVLC files, then reopen the media.")
            });
    }

    private void UpdateNative(Func<PlaybackSnapshot, PlaybackSnapshot> transform) => Update(transform, native: true);

    private void Update(Func<PlaybackSnapshot, PlaybackSnapshot> transform, bool native = false)
    {
        PlaybackSnapshot next;
        lock (_snapshotGate)
        {
            if (_disposed || (native && !_acceptNativeEvents)) return;
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
        _playRequested = false;
        _player.Playing -= OnPlaying;
        _player.Paused -= OnPaused;
        _player.Stopped -= OnStopped;
        _player.EndReached -= OnEnded;
        _player.EncounteredError -= OnError;
        _player.TimeChanged -= OnTime;
        _player.LengthChanged -= OnLength;
        _player.SeekableChanged -= OnSeekable;
        _player.ESAdded -= OnElementaryStreamAdded;
        _player.ESDeleted -= OnSubtitleStreamDeleted;
        _player.ESSelected -= OnSubtitleStreamSelected;
        _player.Vout -= OnVideoOutput;
        Changed = null;
        _runtime.Log -= OnNativeLog;
        _player.Stop();
        _player.Hwnd = 0;
        _player.Dispose();
        _media?.Dispose();
        _runtime.Dispose();
    }
}
