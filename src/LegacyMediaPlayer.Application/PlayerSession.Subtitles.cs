using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

public sealed partial class PlayerSession
{
    private SubtitleSelection? _subtitleChoice;
    private long _subtitleOperation;
    private readonly CancellationTokenSource _subtitleLifetime = new();
    // An abandoned filesystem call retains this gate until it actually returns.
    private readonly SemaphoreSlim _subtitleValidationGate = new(1, 1);
    public long SubtitleInputRevision => _generation;
    public bool CanUseSubtitles => !_disposed && CurrentItem is not null && _engine.Capabilities.SubtitleTracks && Snapshot.HasVideo &&
        Snapshot.State is PlaybackState.Playing or PlaybackState.Paused;

    public void SelectSubtitle(int id)
    {
        _subtitleOperation++;
        Execute(() =>
        {
            if (!CanUseSubtitles) throw new InvalidOperationException("Start a local video before choosing subtitles.");
            var choice = id == -1 ? SubtitleSelection.Off : SubtitleSelection.Track(id,
                Snapshot.SubtitleTracks.SingleOrDefault(t => t.Id == id)?.Name ?? throw new ArgumentException("The subtitle track is no longer available."));
            _engine.SetSubtitles(choice);
            if (_engine.Snapshot.SubtitleError is null) _subtitleChoice = choice;
        });
    }

    /// <summary>Await on the owning context. Only validation runs on the worker.</summary>
    public async Task<bool> LoadSubtitleAsync(string path, CancellationToken cancellationToken = default,
        Func<string, SubtitleFile>? validateFile = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanUseSubtitles) { ReportError("Start a local video before opening subtitles."); return false; }
        var item = CurrentItem; var input = _generation; var operation = ++_subtitleOperation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _subtitleLifetime.Token);
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
                if (_engine.Snapshot.SubtitleError is null) { _subtitleChoice = choice; accepted = true; }
            });
            return accepted;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException)
        { if (IsCurrent()) ReportError($"Could not open subtitle: {ex.Message}"); return false; }
        bool IsCurrent() => !_disposed && !token.IsCancellationRequested && input == _generation &&
            operation == _subtitleOperation && ReferenceEquals(item, CurrentItem);
    }
}
