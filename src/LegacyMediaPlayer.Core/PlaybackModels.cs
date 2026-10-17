namespace LegacyMediaPlayer.Core;

public enum PlaybackState { Empty, Ready, Playing, Paused, Stopped, Ended, Failed }
public enum PlaybackTrackKind { Audio, Subtitle }
public enum VideoSurfaceKind { WindowsHwnd }

public sealed record PlaybackCapabilities(bool Video, bool AudioTracks, bool SubtitleTracks);
public sealed record PlaybackTrack(int Id, PlaybackTrackKind Kind, string Name);
public sealed record VideoSurface(VideoSurfaceKind Kind, nint Handle);
public sealed record PlaybackError(string Code, string Message);

public sealed record PlaybackSnapshot(
    PlaybackState State,
    MediaSource? Media,
    TimeSpan Position,
    TimeSpan Duration,
    bool CanSeek,
    int Volume,
    bool IsMuted,
    PlaybackError? Error)
{
    // Per-input state, independent of whether the backend can render video.
    public bool HasVideo { get; init; }
    public bool HasVideoOutput { get; init; }

    public static PlaybackSnapshot Empty { get; } = new(
        PlaybackState.Empty, null, TimeSpan.Zero, TimeSpan.Zero, false, 75, false, null);
}

public sealed class PlaybackChangedEventArgs(PlaybackSnapshot snapshot) : EventArgs
{
    public PlaybackSnapshot Snapshot { get; } = snapshot;
}

public sealed class PlaybackException(string message, Exception? inner = null)
    : Exception(message, inner);
