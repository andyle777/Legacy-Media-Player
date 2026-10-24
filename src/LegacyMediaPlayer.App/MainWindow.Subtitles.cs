using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

public partial class MainWindow
{
    private sealed record SubtitleOption(int Id, string Label) { public override string ToString() => Label; }
    private SubtitleOption[] _subtitleOptions = [];
    private bool _subtitlePickerOpen;
    private bool _subtitleLoading;

    private void InitializeSubtitles()
    {
        SubtitleSelector.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && !_closed && SubtitleSelector.SelectedItem is SubtitleOption option)
                _session.SelectSubtitle(option.Id);
        };
    }

    private async void OnOpenSubtitle(object? sender, RoutedEventArgs e)
    {
        if (_closed || PlaylistBusy || _subtitlePickerOpen || !_session.CanUseSubtitles) return;
        var revision = _session.SubtitleInputRevision;
        var item = _session.CurrentItem;
        _subtitlePickerOpen = true;
        try
        {
            Refresh();
            var files = await StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Open local SRT subtitle", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("SubRip subtitle (SRT)") { Patterns = ["*.srt"] }]
            });
            if (!_closed && !_savingSession && revision == _session.SubtitleInputRevision &&
                ReferenceEquals(item, _session.CurrentItem) && files.Count > 0)
                await LoadSubtitleAsync(files[0].TryGetLocalPath() ?? string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { if (!_closed && revision == _session.SubtitleInputRevision) _session.ReportError($"Could not open subtitles: {ex.Message}"); }
        finally { _subtitlePickerOpen = false; Refresh(); }
    }

    public async Task<bool> LoadSubtitleAsync(string path)
    {
        if (_closed || _savingSession) return false;
        _subtitleLoading = true; Refresh();
        try { return await _session.LoadSubtitleAsync(path, _playlistLifetime.Token); }
        finally { _subtitleLoading = false; Refresh(); }
    }

    private void RefreshSubtitles()
    {
        var snapshot = _session.Snapshot;
        var options = new[] { new SubtitleOption(-1, "Off") }.Concat(snapshot.SubtitleTracks.Select(t =>
        {
            var name = string.IsNullOrWhiteSpace(t.Name) ? $"Subtitle {t.Id}" : t.Name;
            return new SubtitleOption(t.Id, snapshot.SubtitleTracks.Count(other => other.Name == t.Name) > 1 ? $"{name} (ID {t.Id})" : name);
        })).ToArray();
        if (!_subtitleOptions.SequenceEqual(options))
        { _subtitleOptions = options; SubtitleSelector.ItemsSource = options; }
        SubtitleSelector.SelectedIndex = Array.FindIndex(options, option => option.Id == snapshot.SelectedSubtitleId);
        SubtitleSelector.IsEnabled = !PlaylistBusy && _session.CanUseSubtitles;
        OpenSubtitleButton.IsEnabled = SubtitleSelector.IsEnabled && !_subtitlePickerOpen && !_subtitleLoading;
        SubtitleStatusLabel.Text = _subtitleLoading || snapshot.SubtitlePending ? "Loading subtitles…" :
            snapshot.SubtitleError is not null ? "Unavailable" : "";
        ToolTip.SetTip(SubtitleSelector, snapshot.SubtitleError?.Message ??
            (_session.CanUseSubtitles ? "Choose a subtitle track or Off. Escape exits fullscreen to change subtitles." : "Subtitles are available during video playback."));
    }
}
