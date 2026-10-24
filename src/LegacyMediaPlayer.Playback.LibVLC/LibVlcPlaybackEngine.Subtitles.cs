using System.Diagnostics;
using LegacyMediaPlayer.Core;
using LibVLCSharp.Shared;

namespace LegacyMediaPlayer.Playback.LibVLC;

public sealed partial class LibVlcPlaybackEngine
{
    // Owner-thread state only; native callbacks merely set the snapshot refresh flag.
    private SubtitleSelection? _subtitleRequest;
    private bool _subtitleApplied;
    private long _subtitleClock;
    private TimeSpan _subtitleElapsed;
    private HashSet<int> _beforeAttachment = [];
    private int? _subtitleTarget;
    private long _subtitleRefreshVersion;

    private void ResetSubtitles()
    {
        _subtitleRequest = null; _subtitleApplied = false; _subtitleClock = 0; _subtitleElapsed = TimeSpan.Zero;
        _beforeAttachment.Clear();
        _subtitleTarget = null;
        Update(s => s with { SubtitleTracks = Array.Empty<PlaybackTrack>(), SelectedSubtitleId = -1,
            SubtitlePending = false, SubtitleRefreshRequired = false, SubtitleError = null });
    }

    public void SetSubtitles(SubtitleSelection selection)
    {
        RequireMedia(); ArgumentNullException.ThrowIfNull(selection);
        // Ready is allowed for restoring the same current item's choice before Play.
        if (Snapshot.State is not PlaybackState.Ready && !Snapshot.HasVideo)
        { SubtitleFailure("Subtitles are available only for video."); return; }
        var previous = _subtitleRequest;
        try
        {
            _beforeAttachment = Snapshot.SubtitleTracks.Select(t => t.Id).ToHashSet();
            if (selection.File is { } file)
            {
                // A file can disappear after worker validation; native rejection remains nonfatal.
                if (!File.Exists(file.Path)) throw new FileNotFoundException("The subtitle file no longer exists.", file.Path);
                var accepted = Snapshot.State is PlaybackState.Ready or PlaybackState.Stopped
                    ? _media!.AddSlave(MediaSlaveType.Subtitle, 4, file.Uri)
                    : _player.AddSlave(MediaSlaveType.Subtitle, file.Uri.AbsoluteUri, true);
                if (!accepted) throw new PlaybackException("The playback engine rejected the subtitle file.");
            }
            _subtitleRequest = selection;
            _subtitleTarget = selection.TrackId;
            _subtitleApplied = selection.File is not null;
            _subtitleClock = 0; _subtitleElapsed = TimeSpan.Zero;
            Update(s => s with { SubtitlePending = true, SubtitleError = null, SubtitleRefreshRequired = true });
            RefreshSubtitles();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlaybackException)
        { _subtitleRequest = previous; SubtitleFailure($"Could not load subtitles: {ex.Message}"); }
    }

    public void RefreshSubtitles()
    {
        CheckDisposed();
        var snapshot = Snapshot;
        var refreshVersion = Interlocked.Read(ref _subtitleRefreshVersion);
        if (_media is null || snapshot.State is PlaybackState.Stopped or PlaybackState.Ended or PlaybackState.Failed)
        { if (snapshot.SubtitleRefreshRequired) Update(s => s with { SubtitleRefreshRequired = false }); return; }
        // Calls are made by the serialized owner after dispatch, not by ES callbacks.
        var tracks = Array.AsReadOnly(_player.SpuDescription.Where(t => t.Id >= 0)
            .Select(t => new PlaybackTrack(t.Id, PlaybackTrackKind.Subtitle, SafeSubtitleName(t.Name, t.Id))).ToArray());
        var selected = _player.Spu;
        var pending = snapshot.SubtitlePending;
        if (pending && snapshot.State is PlaybackState.Playing or PlaybackState.Paused && snapshot.HasVideo && _subtitleRequest is { } request)
        {
            int? target = _subtitleTarget;
            if (!_subtitleApplied && target is null && request.TrackName is { } name)
            {
                var matches = tracks.Where(t => t.Name == name).ToArray();
                if (matches.Length > 1) { SubtitleFailure("The previous subtitle choice is ambiguous. Choose a track again."); pending = false; }
                else if (matches.Length == 1) target = _subtitleTarget = matches[0].Id;
            }
            if (!_subtitleApplied && target is { } id && (id == -1 || tracks.Any(t => t.Id == id)))
            {
                if (selected == id || _player.SetSpu(id)) _subtitleApplied = true;
                else { SubtitleFailure("The playback engine rejected subtitle selection."); pending = false; }
                selected = _player.Spu;
            }
            if (_subtitleApplied && (request.File is not null ? selected >= 0 && !_beforeAttachment.Contains(selected) :
                target is { } requested && selected == requested)) pending = false;
            if (snapshot.State == PlaybackState.Playing)
            {
                var now = Stopwatch.GetTimestamp();
                if (_subtitleClock != 0) _subtitleElapsed += Stopwatch.GetElapsedTime(_subtitleClock, now);
                _subtitleClock = now;
                if (pending && _subtitleElapsed >= TimeSpan.FromSeconds(5))
                { SubtitleFailure("Subtitles were not confirmed. The file or track may be unavailable or unsupported. Try another SRT or select Off."); pending = false; }
            }
            else _subtitleClock = 0;
        }
        Update(s => s with { SubtitleTracks = tracks, SelectedSubtitleId = selected, SubtitlePending = pending,
            SubtitleRefreshRequired = Interlocked.Read(ref _subtitleRefreshVersion) != refreshVersion });
    }

    private void SubtitleFailure(string message) => Update(s => s with
    { SubtitlePending = false, SubtitleRefreshRequired = false, SubtitleError = new("subtitle-unavailable", message) });

    private static string SafeSubtitleName(string? name, int id)
    {
        var clean = new string((name ?? "").Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
        return clean.Length == 0 ? $"Subtitle {id}" : clean;
    }

    private void RequestSubtitleRefresh()
    {
        Interlocked.Increment(ref _subtitleRefreshVersion);
        UpdateNative(s => s with { SubtitleRefreshRequired = true });
    }
    private void OnSubtitleStreamDeleted(object? sender, MediaPlayerESDeletedEventArgs e) { if (e.Type == TrackType.Text) RequestSubtitleRefresh(); }
    private void OnSubtitleStreamSelected(object? sender, MediaPlayerESSelectedEventArgs e) { if (e.Type == TrackType.Text) RequestSubtitleRefresh(); }
}
