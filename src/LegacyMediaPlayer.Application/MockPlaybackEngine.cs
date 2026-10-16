using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

/// <summary>Deterministic development engine. Does not decode media or advance time.</summary>
public sealed class MockPlaybackEngine : IPlaybackEngine
{
    private bool _disposed;
    public PlaybackCapabilities Capabilities { get; } = new(false, false, false);
    public PlaybackSnapshot Snapshot { get; private set; } = PlaybackSnapshot.Empty;
    public event EventHandler<PlaybackChangedEventArgs>? Changed;
    public void Open(MediaSource source)
    {
        CheckDisposed();
        ArgumentNullException.ThrowIfNull(source);
        if (!File.Exists(source.Path)) throw new FileNotFoundException("The media file no longer exists.", source.Path);
        Snapshot = Snapshot with
        {
            Media = source,
            State = PlaybackState.Ready,
            Position = TimeSpan.Zero,
            Duration = TimeSpan.FromMinutes(3),
            CanSeek = true,
            Error = null
        };
        Publish();
    }

    public void Play() { RequireMedia(); Snapshot = Snapshot with { State = PlaybackState.Playing }; Publish(); }
    public void Pause()
    {
        RequireMedia();
        if (Snapshot.State != PlaybackState.Playing) return;
        Snapshot = Snapshot with { State = PlaybackState.Paused };
        Publish();
    }
    public void Stop()
    {
        CheckDisposed();
        if (Snapshot.Media is null) return;
        Snapshot = Snapshot with { State = PlaybackState.Stopped, Position = TimeSpan.Zero };
        Publish();
    }
    public void Seek(TimeSpan position)
    {
        CheckDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(position, TimeSpan.Zero);
        RequireMedia();
        Snapshot = Snapshot with { Position = position > Snapshot.Duration ? Snapshot.Duration : position };
        Publish();
    }
    public void SetVolume(int volume)
    {
        CheckDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 100);
        Snapshot = Snapshot with { Volume = volume };
        Publish();
    }
    public void SetMuted(bool muted) { CheckDisposed(); Snapshot = Snapshot with { IsMuted = muted }; Publish(); }
    public IReadOnlyList<PlaybackTrack> GetTracks() { CheckDisposed(); return []; }
    public void SelectTrack(PlaybackTrackKind kind, int id) { CheckDisposed(); throw new NotSupportedException("Mock mode has no media tracks."); }
    public void AttachVideoSurface(VideoSurface? surface)
    {
        CheckDisposed();
        if (surface is not null) throw new NotSupportedException("Mock mode does not render video.");
    }
    public void Dispose() { _disposed = true; Changed = null; }
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void RequireMedia()
    {
        CheckDisposed();
        if (Snapshot.Media is null) throw new InvalidOperationException("Open a local media file first.");
    }
    private void Publish() => Changed?.Invoke(this, new(Snapshot));
}
