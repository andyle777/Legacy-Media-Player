using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Playback.LibVLC;

internal sealed record NativeSubtitleView(IReadOnlyList<PlaybackTrack> Tracks, int SelectedId, IReadOnlyList<string> SlaveUris);

/// <summary>Serialized owner-only state machine; delegates isolate native effects for fault tests.</summary>
internal sealed class NativeSubtitles(Func<NativeSubtitleView> read, Func<int, bool> select,
    Func<SubtitleFile, bool, bool> attach)
{
    private sealed record Attachment(SubtitleFile File, HashSet<int> Before);
    private readonly Dictionary<int, SubtitleFile> _bindings = [];
    private SubtitleSelection? _intent, _confirmed;
    private Attachment? _attachment;
    private TimeSpan _requestTime, _attachmentTime;
    private PlaybackError? _error;

    internal SubtitleSelection? ReplaySelection => (_confirmed ?? (_bindings.Count > 0 ? SubtitleSelection.Off : null))?
        .ForReplay().WithFiles(_bindings.Values);

    internal void Reset()
    {
        _bindings.Clear(); _intent = _confirmed = null; _attachment = null;
        _requestTime = _attachmentTime = TimeSpan.Zero; _error = null;
    }

    internal void Request(SubtitleSelection selection)
    { _intent = selection; _requestTime = TimeSpan.Zero; _error = null; }

    internal PlaybackSnapshot Refresh(PlaybackSnapshot input, TimeSpan playingElapsed)
    {
        if (!input.HasVideo || input.State is not (PlaybackState.Playing or PlaybackState.Paused))
            return input with { SubtitlePending = input.State == PlaybackState.Ready && _intent is not null,
                SubtitleRefreshRequired = false, SubtitleError = _error };
        var view = read();
        _requestTime += playingElapsed;
        if (_attachment is { } loading)
        {
            _attachmentTime += playingElapsed;
            // Pinned LibVLC updates this list AFTER InputSlaveSourceAdd completes.
            // AddSlave's return merely queues work; it is not attachment identity.
            if (view.SlaveUris.Any(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed == loading.File.Uri))
            {
                var added = view.Tracks.Where(t => !loading.Before.Contains(t.Id)).ToArray();
                if (added.Length == 1) _bindings[added[0].Id] = loading.File;
                else if (_intent?.File == loading.File) Fail("The attached subtitle track could not be identified safely.", false);
                _attachment = null;
            }
            else if (_attachmentTime >= TimeSpan.FromSeconds(5))
            {
                if (_intent?.File == loading.File) Fail("The subtitle file did not produce a confirmed track. Try another SRT or select Off.", false);
                _attachment = null;
            }
        }

        // Only one submitted attachment at a time. A newer intent supersedes the
        // choice immediately; an older non-selecting native load is only retired.
        if (_intent is { } wanted && _attachment is null)
        {
            var files = (wanted.File is { } chosen ? new[] { chosen } : Array.Empty<SubtitleFile>())
                .Concat(wanted.ExternalFiles).Distinct();
            var missing = files.FirstOrDefault(file => !_bindings.ContainsValue(file));
            if (missing is not null)
            {
                if (attach(missing, false))
                { _attachment = new(missing, view.Tracks.Select(t => t.Id).ToHashSet()); _attachmentTime = TimeSpan.Zero; }
                else Fail("The playback engine rejected the subtitle file.", false);
            }
        }

        int? target = null;
        if (_intent is { } request)
        {
            if (request.File is { } source)
            {
                var binding = _bindings.FirstOrDefault(pair => pair.Value == source);
                if (binding.Value is not null) target = binding.Key;
            }
            else if (request.TrackId is { } id)
            {
                if (id == -1 || view.Tracks.Any(t => t.Id == id)) target = id;
                else Fail("The requested subtitle track is no longer available.", false);
            }
            else if (request.TrackName is { } key)
            {
                var matches = view.Tracks.Where(t => t.ReplayKey == key).ToArray();
                if (matches.Length == 1) target = matches[0].Id;
                else if (matches.Length > 1) Fail("The previous subtitle choice is ambiguous. Choose a track again.", true);
            }
            else Fail("The previous subtitle has no reliable metadata identity. Choose a track again.", true);

            if (target is null && _error is not null) target = RecoveryTarget(view);
            if (target is { } requested && view.SelectedId != requested)
            {
                if (!select(requested))
                {
                    Fail("The playback engine rejected subtitle selection.", false);
                    target = RecoveryTarget(view);
                    if (view.SelectedId != target) select(target.Value);
                }
                view = read();
            }
            if (target is { } accepted && view.SelectedId == accepted && _error is null)
                _confirmed = accepted == -1 ? SubtitleSelection.Off : _bindings.TryGetValue(accepted, out var file) ?
                    SubtitleSelection.External(file) : SubtitleSelection.Track(accepted, view.Tracks.First(t => t.Id == accepted).ReplayKey);
        }

        var pending = _attachment is not null || _intent is not null && (target is null || view.SelectedId != target);
        if (pending && _requestTime >= TimeSpan.FromSeconds(5))
        {
            Fail("Subtitles were not confirmed. Choose an available track or another SRT.", _intent?.File is null && _intent?.TrackId is null);
            _attachment = null; target = RecoveryTarget(view);
            if (view.SelectedId != target) select(target.Value);
            view = read(); pending = view.SelectedId != target;
        }
        return input with
        {
            SubtitleTracks = Array.AsReadOnly(view.Tracks.Select(t => t with
                { ExternalSubtitle = _bindings.GetValueOrDefault(t.Id) }).ToArray()),
            SelectedSubtitleId = view.SelectedId, SubtitlePending = pending,
            SubtitleError = _error, SubtitleRefreshRequired = false
        };
    }

    private void Fail(string message, bool turnOff)
    {
        _error = new("subtitle-unavailable", message);
        if (turnOff) _confirmed = SubtitleSelection.Off;
        _intent = _confirmed ?? SubtitleSelection.Off;
    }

    private int RecoveryTarget(NativeSubtitleView view)
    {
        if (_intent?.File is { } file)
        {
            var match = _bindings.FirstOrDefault(p => p.Value == file && view.Tracks.Any(t => t.Id == p.Key));
            if (match.Value is not null) return match.Key;
        }
        return _intent?.TrackId is { } id && view.Tracks.Any(t => t.Id == id) ? id : -1;
    }
}
