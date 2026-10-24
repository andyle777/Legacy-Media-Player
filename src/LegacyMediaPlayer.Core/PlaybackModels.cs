namespace LegacyMediaPlayer.Core;

public enum PlaybackState { Empty, Ready, Playing, Paused, Stopped, Ended, Failed }
public enum PlaybackTrackKind { Audio, Subtitle }
public enum VideoSurfaceKind { WindowsHwnd }

public sealed record PlaybackCapabilities(bool Video, bool AudioTracks, bool SubtitleTracks);
public sealed record PlaybackTrack(int Id, PlaybackTrackKind Kind, string Name)
{
    // A real metadata identity, never a generated display label or prior native ID.
    public string? ReplayKey { get; init; } = Name;
    public SubtitleFile? ExternalSubtitle { get; init; }
}
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
    /// <summary>User preference, retained across media changes; 1 means normal speed.</summary>
    public float Rate { get; init; } = 1f;
    /// <summary>Native accepted/requested rate, not a measurement of real output speed.
    /// Null until application to an active input has been attempted.</summary>
    public float? AcceptedRate { get; init; }
    public PlaybackError? RateError { get; init; }
    public IReadOnlyList<PlaybackTrack> SubtitleTracks { get; init; } = Array.Empty<PlaybackTrack>();
    public int SelectedSubtitleId { get; init; } = -1;
    public bool SubtitlePending { get; init; }
    public bool SubtitleRefreshRequired { get; init; }
    public PlaybackError? SubtitleError { get; init; }

    public static PlaybackSnapshot Empty { get; } = new(
        PlaybackState.Empty, null, TimeSpan.Zero, TimeSpan.Zero, false, 75, false, null);
}

public sealed class PlaybackChangedEventArgs(PlaybackSnapshot snapshot) : EventArgs
{
    public PlaybackSnapshot Snapshot { get; } = snapshot;
}

public sealed class PlaybackException(string message, Exception? inner = null)
    : Exception(message, inner);
