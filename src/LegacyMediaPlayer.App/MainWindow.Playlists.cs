using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

public partial class MainWindow
{
    private readonly IPlaylistFileService _playlistFiles;
    private readonly string? _sessionPlaylistPath;
    private readonly CancellationTokenSource _playlistLifetime = new();
    private bool _playlistPickerOpen;
    private bool _restoringSession;
    private bool _canPersistSession;
    private bool _queueEdited;
    private bool _savingSession;
    private bool _allowSessionClose;
    private string? _playlistOperation;

    private bool PlaylistBusy => _closed || _opening || _playlistPickerOpen || _savingSession;
    private static FilePickerFileType M3u8Type => new("UTF-8 playlist (M3U8)") { Patterns = ["*.m3u8"] };
    private static FilePickerFileType M3uType => new("Playlist (M3U)") { Patterns = ["*.m3u"] };
    private static bool PlaylistError(Exception ex) => ex is ArgumentException or IOException or
        UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException;

    private async void OnOpenPlaylist(object? sender, RoutedEventArgs e) => await PickPlaylistAsync(save: false);
    private async void OnSavePlaylist(object? sender, RoutedEventArgs e) => await PickPlaylistAsync(save: true);

    private async Task PickPlaylistAsync(bool save)
    {
        if (PlaylistBusy) return;
        _playlistPickerOpen = true; Refresh();
        string? path = null;
        try
        {
            if (save)
            {
                var file = await StorageProvider.SaveFilePickerAsync(new()
                {
                    Title = "Save playlist", SuggestedFileName = "playlist.m3u8", DefaultExtension = "m3u8",
                    FileTypeChoices = [M3u8Type, M3uType], ShowOverwritePrompt = true
                });
                path = file?.TryGetLocalPath();
            }
            else
            {
                var files = await StorageProvider.OpenFilePickerAsync(new()
                {
                    Title = "Open playlist (replace queue; wait for Play)", AllowMultiple = false,
                    FileTypeFilter = [new("Playlists (M3U, M3U8)") { Patterns = ["*.m3u", "*.m3u8"] }]
                });
                path = files.FirstOrDefault()?.TryGetLocalPath();
            }
        }
        catch (Exception ex) when (PlaylistError(ex))
        { if (!_closed) _session.ReportError($"Could not open the playlist picker: {ex.Message}"); }
        finally { _playlistPickerOpen = false; Refresh(); }
        if (path is null || _closed) return; // Picker cancellation never changes the queue.
        if (save) await SavePlaylistAsync(path); else await OpenPlaylistAsync(path);
    }

    public Task OpenPlaylistAsync(string path, CancellationToken cancellationToken = default) =>
        LoadPlaylistAsync(path, restoring: false, cancellationToken);

    public Task RestoreQueueAsync() => _sessionPlaylistPath is { } path
        ? LoadPlaylistAsync(path, restoring: true, CancellationToken.None) : Task.CompletedTask;

    private async Task LoadPlaylistAsync(string path, bool restoring, CancellationToken cancellationToken)
    {
        if (PlaylistBusy) return;
        _opening = true; _restoringSession = restoring; _playlistOperation = "Loading playlist…"; Refresh();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_playlistLifetime.Token, cancellationToken);
        try
        {
            var result = await _playlistFiles.LoadAsync(path, lifetime.Token);
            if (_closed || _savingSession || lifetime.IsCancellationRequested) return;
            if (result.EntryCount > 0 && result.Paths.Count == 0)
            {
                _session.ReportError($"No supported readable local media in the playlist. Skipped {result.SkippedCount} item(s); queue unchanged.");
                return;
            }
            if (!_session.ReplaceQueue(result.Paths)) return;
            QueueList.SelectedIndex = -1;
            if (restoring) _canPersistSession = true;
            if (result.SkippedCount > 0) _session.ReportError($"Skipped {result.SkippedCount} missing, unsupported or nonlocal playlist item(s).");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (restoring && ex is FileNotFoundException or DirectoryNotFoundException) { _canPersistSession = true; }
        catch (Exception ex) when (PlaylistError(ex))
        { if (!_closed && !_savingSession) _session.ReportError($"Could not load playlist: {ex.Message}"); }
        finally { _restoringSession = false; _opening = false; if (!_savingSession) _playlistOperation = null; Refresh(); }
    }

    public async Task SavePlaylistAsync(string path, CancellationToken cancellationToken = default)
    {
        if (PlaylistBusy) return;
        var paths = _session.Queue.Select(item => item.Source.Path).ToArray();
        var relative = RelativePlaylistPaths.IsChecked == true;
        _opening = true; _playlistOperation = "Saving playlist…"; Refresh();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_playlistLifetime.Token, cancellationToken);
        try { await _playlistFiles.SaveAsync(path, paths, relative, lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (PlaylistError(ex))
        { if (!_closed && !_savingSession) _session.ReportError($"Could not save playlist: {ex.Message}"); }
        finally { _opening = false; if (!_savingSession) _playlistOperation = null; Refresh(); }
    }

    private async void OnPersistQueueClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.Cancel || _allowSessionClose || _closed) return;
        // A pending/failed restore must not overwrite the prior snapshot with an empty queue.
        if (_sessionPlaylistPath is null || (!_canPersistSession && !_queueEdited)) return;
        e.Cancel = true;
        if (_savingSession) return;
        _savingSession = true;
        _playlistOperation = "Saving previous queue…";
        _playlistLifetime.Cancel();
        var paths = _session.Queue.Select(item => item.Source.Path).ToArray();
        Refresh();
        try { await _playlistFiles.SaveAsync(_sessionPlaylistPath, paths, relativePaths: false); }
        catch (Exception ex) when (PlaylistError(ex) || ex is OperationCanceledException)
        { _session.ReportError($"The previous queue could not be saved: {ex.Message}"); }
        finally { _allowSessionClose = true; Close(); }
    }

    private async Task RunTransportAsync(Action command)
    {
        if (PlaylistBusy) return;
        _opening = true; Refresh();
        try
        {
            if (_session.Queue.Count > 0 && _preparePlayback is not null) await _preparePlayback();
            if (!_closed && !_savingSession) command();
        }
        catch (Exception ex) when (PlaylistError(ex))
        { if (!_closed && !_savingSession) _session.ReportError($"Playback could not start: {ex.Message}"); }
        finally { _opening = false; Refresh(); }
    }
}
