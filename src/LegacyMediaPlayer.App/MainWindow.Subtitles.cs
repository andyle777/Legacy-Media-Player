using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

public partial class MainWindow
{
    private sealed record SubtitleOption(int Id, string Label) { public override string ToString() => Label; }
    private SubtitleOption[] _subtitleOptions = [];
    private bool _subtitlePickerOpen;
    private readonly Func<Task<string?>>? _pickSubtitlePath;
    private bool _subtitleLoading;
    private long _subtitleUiOperation;
    private readonly DispatcherTimer _subtitleRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private void InitializeSubtitles()
    {
        _subtitleRefreshTimer.Tick += (_, _) => { if (!_closed) _session.RefreshSubtitleState(); };
        Closed += (_, _) => _subtitleRefreshTimer.Stop();
        SubtitleSelector.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && !_closed && SubtitleSelector.SelectedItem is SubtitleOption option)
                _session.SelectSubtitle(option.Id);
        };
    }

    private async void OnOpenSubtitle(object? sender, RoutedEventArgs e) => await PickSubtitleAsync();

    public async Task PickSubtitleAsync()
    {
        if (_closed || PlaylistBusy || _subtitlePickerOpen || !_session.CanUseSubtitles) return;
        var revision = _session.SubtitleContextRevision;
        var item = _session.CurrentItem;
        _subtitlePickerOpen = true;
        StartupTiming.Mark("subtitle.picker.begin");
        try
        {
            Refresh();
            var path = await (_pickSubtitlePath?.Invoke() ?? PickNativeSubtitlePathAsync());
            StartupTiming.Mark(path is null ? "subtitle.picker.cancelled" : "subtitle.picker.selected");
            if (!_closed && !_savingSession && revision == _session.SubtitleContextRevision &&
                ReferenceEquals(item, _session.CurrentItem) && path is not null)
                await LoadSubtitleAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { if (!_closed && revision == _session.SubtitleContextRevision) _session.ReportError($"Could not open subtitles: {ex.Message}"); }
        finally { _subtitlePickerOpen = false; StartupTiming.Mark("subtitle.picker.end"); Refresh(); }
    }

    private async Task<string?> PickNativeSubtitlePathAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Open local SRT subtitle", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SubRip subtitle (SRT)") { Patterns = ["*.srt"] }]
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() ?? string.Empty : null;
    }

    public async Task<bool> LoadSubtitleAsync(string path)
    {
        if (_closed || _savingSession) return false;
        var operation = ++_subtitleUiOperation;
        StartupTiming.Mark("subtitle.attachment.begin");
        _subtitleLoading = true; Refresh();
        try { return await _session.LoadSubtitleAsync(path, _playlistLifetime.Token); }
        finally { if (operation == _subtitleUiOperation) _subtitleLoading = false; StartupTiming.Mark("subtitle.attachment.end"); Refresh(); }
    }

    private void RefreshSubtitles()
    {
        var snapshot = _session.Snapshot;
        if (snapshot.SubtitlePending) _subtitleRefreshTimer.Start(); else _subtitleRefreshTimer.Stop();
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
