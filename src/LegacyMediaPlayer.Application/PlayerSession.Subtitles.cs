using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

public sealed partial class PlayerSession
{
    private SubtitleSelection? _subtitleChoice;
    private SubtitleSelection? _pendingSubtitleChoice;
    private readonly HashSet<SubtitleFile> _currentSubtitleFiles = [];
    private CancellationTokenSource? _subtitleCancellation;
    private long _subtitleOperation;
    private readonly CancellationTokenSource _subtitleLifetime = new();
    // An abandoned filesystem call retains this gate until it actually returns.
    private readonly SemaphoreSlim _subtitleValidationGate = new(1, 1);
    // A file choice belongs to a logical queue item, not one decoder input/loop.
    public long SubtitleContextRevision => _subtitleOperation;
    public bool CanUseSubtitles => !_disposed && CurrentItem is not null && _engine.Capabilities.SubtitleTracks && Snapshot.HasVideo &&
        Snapshot.State is PlaybackState.Playing or PlaybackState.Paused;
    public void RefreshSubtitleState() { if (!_disposed && Snapshot.SubtitlePending) Execute(_engine.RefreshSubtitles); }

    public void SelectSubtitle(int id)
    {
        CancelSubtitleWork();
        Execute(() =>
        {
            if (!CanUseSubtitles) throw new InvalidOperationException("Start a local video before choosing subtitles.");
            var track = Snapshot.SubtitleTracks.SingleOrDefault(t => t.Id == id);
            if (id != -1 && track is null) throw new ArgumentException("The subtitle track is no longer available.");
            var choice = id == -1 ? SubtitleSelection.Off : track!.ExternalSubtitle is { } file ?
                SubtitleSelection.External(file) : SubtitleSelection.Track(id, track.ReplayKey);
            _engine.SetSubtitles(choice);
            _pendingSubtitleChoice = choice;
            RememberConfirmedSubtitles(_engine.Snapshot);
        });
    }

    /// <summary>Await on the owning context. Only validation runs on the worker.</summary>
    public async Task<bool> LoadSubtitleAsync(string path, CancellationToken cancellationToken = default,
        Func<string, SubtitleFile>? validateFile = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanUseSubtitles) { ReportError("Start a local video before opening subtitles."); return false; }
        CancelSubtitleWork();
        var item = CurrentItem; var operation = _subtitleOperation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _subtitleLifetime.Token);
        _subtitleCancellation = linked;
        var token = linked.Token;
        try
        {
            var source = await Task.Run(async () =>
            {
                await _subtitleValidationGate.WaitAsync(token).ConfigureAwait(false);
                try { token.ThrowIfCancellationRequested(); return (validateFile ?? SubtitleFile.FromFile)(path); }
                finally { _subtitleValidationGate.Release(); }
            }, token).WaitAsync(token);
            if (!IsCurrent()) return false;
            var accepted = false;
            Execute(() =>
            {
                var choice = SubtitleSelection.External(source);
                _engine.SetSubtitles(choice);
                _pendingSubtitleChoice = choice;
                RememberConfirmedSubtitles(_engine.Snapshot);
                accepted = _engine.Snapshot.SubtitleError is null;
            });
            return accepted;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException)
        { if (IsCurrent()) ReportError($"Could not open subtitle: {ex.Message}"); return false; }
        finally { if (ReferenceEquals(_subtitleCancellation, linked)) _subtitleCancellation = null; }
        bool IsCurrent() => !_disposed && !token.IsCancellationRequested &&
            operation == _subtitleOperation && ReferenceEquals(item, CurrentItem);
    }

    private void CancelSubtitleWork()
    {
        _subtitleOperation++;
        _subtitleCancellation?.Cancel();
        _pendingSubtitleChoice = null;
    }

    private void RememberConfirmedSubtitles(PlaybackSnapshot snapshot)
    {
        foreach (var track in snapshot.SubtitleTracks)
            if (track.ExternalSubtitle is { } file) _currentSubtitleFiles.Add(file);
        if (_pendingSubtitleChoice is not { } choice) return;
        if (snapshot.SubtitleError is not null) { _pendingSubtitleChoice = null; return; }
        var selected = snapshot.SubtitleTracks.FirstOrDefault(t => t.Id == snapshot.SelectedSubtitleId);
        var confirmed = choice.File is { } source ? selected?.ExternalSubtitle == source :
            choice.TrackId is { } id ? id == snapshot.SelectedSubtitleId :
            choice.TrackName is { } key && selected?.ReplayKey == key;
        if (confirmed) { _subtitleChoice = choice; _pendingSubtitleChoice = null; }
    }
}
