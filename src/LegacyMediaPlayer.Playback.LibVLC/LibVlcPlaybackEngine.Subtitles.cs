using System.Diagnostics;
using LegacyMediaPlayer.Core;
using LibVLCSharp.Shared;

namespace LegacyMediaPlayer.Playback.LibVLC;

public sealed partial class LibVlcPlaybackEngine
{
    private NativeSubtitles _subtitles = null!;
    private SubtitleSelection? _subtitleReplay;
    private long _subtitleClock, _subtitleRefreshVersion;

    private void InitializeSubtitles() => _subtitles = new(ReadSubtitleView, id => _player.SetSpu(id),
        (file, force) => _player.AddSlave(MediaSlaveType.Subtitle, file.Uri.AbsoluteUri, force));

    private NativeSubtitleView ReadSubtitleView()
    {
        var metadata = _media!.Tracks.Where(t => t.TrackType == TrackType.Text).GroupBy(t => t.Id)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        var tracks = _player.SpuDescription.Where(t => t.Id >= 0).Select(t =>
        {
            metadata.TryGetValue(t.Id, out var detail);
            var description = detail.Description; var language = detail.Language;
            var key = !string.IsNullOrWhiteSpace(description) || !string.IsNullOrWhiteSpace(language) && language != "und"
                ? $"{description}\u001f{language}" : null;
            var name = new string((t.Name ?? "").Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
            return new PlaybackTrack(t.Id, PlaybackTrackKind.Subtitle, name.Length == 0 ? $"Subtitle {t.Id}" : name) { ReplayKey = key };
        }).ToArray();
        return new(Array.AsReadOnly(tracks), _player.Spu, _media.Slaves.Select(s => s.Uri).ToArray());
    }

    private void ResetSubtitles()
    {
        _subtitles.Reset(); _subtitleReplay = null; _subtitleClock = 0;
        Update(s => s with { SubtitleTracks = Array.Empty<PlaybackTrack>(), SelectedSubtitleId = -1,
            SubtitlePending = false, SubtitleRefreshRequired = false, SubtitleError = null });
    }

    public void SetSubtitles(SubtitleSelection selection)
    {
        RequireMedia(); ArgumentNullException.ThrowIfNull(selection);
        _subtitles.Request(selection);
        Update(s => s with { SubtitlePending = true, SubtitleError = null });
        RefreshSubtitles();
    }

    public void RefreshSubtitles()
    {
        CheckDisposed();
        if (_media is null) return;
        var snapshot = Snapshot;
        var version = Interlocked.Read(ref _subtitleRefreshVersion);
        var now = Stopwatch.GetTimestamp();
        var elapsed = snapshot.State == PlaybackState.Playing && _subtitleClock != 0 ? Stopwatch.GetElapsedTime(_subtitleClock, now) : TimeSpan.Zero;
        _subtitleClock = snapshot.State == PlaybackState.Playing ? now : 0;
        var result = _subtitles.Refresh(snapshot, elapsed);
        Update(s => s with { SubtitleTracks = result.SubtitleTracks, SelectedSubtitleId = result.SelectedSubtitleId,
            SubtitlePending = result.SubtitlePending, SubtitleError = result.SubtitleError,
            SubtitleRefreshRequired = Interlocked.Read(ref _subtitleRefreshVersion) != version });
    }

    private void RequestSubtitleRefresh()
    {
        Interlocked.Increment(ref _subtitleRefreshVersion);
        UpdateNative(s => s with { SubtitleRefreshRequired = true });
    }
    private void OnSubtitleStreamDeleted(object? sender, MediaPlayerESDeletedEventArgs e) { if (e.Type == TrackType.Text) RequestSubtitleRefresh(); }
    private void OnSubtitleStreamSelected(object? sender, MediaPlayerESSelectedEventArgs e) { if (e.Type == TrackType.Text) RequestSubtitleRefresh(); }
}
