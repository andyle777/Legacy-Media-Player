namespace LegacyMediaPlayer.Core;

/// <summary>
/// Caller serializes commands and disposal. Changed may run on a native thread:
/// dispatch notifications before issuing any command or updating a view.
/// Snapshot is immutable and safe to read from any thread.
/// </summary>
public interface IPlaybackEngine : IDisposable
{
    PlaybackCapabilities Capabilities { get; }
    PlaybackSnapshot Snapshot { get; }
    event EventHandler<PlaybackChangedEventArgs>? Changed;
    void Open(MediaSource source);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(int volume);
    void SetMuted(bool muted);
    /// <summary>Save the preference without starting playback; reject invalid rates.
    /// A native limitation is reported in Snapshot.RateError without failing playback.</summary>
    void SetRate(float rate);
    IReadOnlyList<PlaybackTrack> GetTracks();
    void SelectTrack(PlaybackTrackKind kind, int id);
    /// <summary>Attach before playback; pass null before destroying the native window.</summary>
    void AttachVideoSurface(VideoSurface? surface);
}
