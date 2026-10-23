using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.UiTests;

public partial class PlayerWindowTests
{
    [AvaloniaFact]
    public async Task Playlist_import_is_idle_preserves_duplicates_and_preferences_without_initializing_codecs()
    {
        using var f = new PlaylistWindowFiles(); var first = f.Media("Việt Nam.mp3"); var second = f.Media("two.flac");
        var path = f.Playlist(first, second, first); var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        var window = new MainWindow(engine, false, engine.InitializeAsync, preferences: new(37, true, RepeatMode.All, true, 1.25f));
        try
        {
            window.Show(); await window.OpenPlaylistAsync(path); Dispatcher.UIThread.RunJobs();
            var list = window.FindControl<ListBox>("QueueList")!;
            Assert.Equal(3, list.ItemCount); Assert.Equal(-1, list.SelectedIndex);
            Assert.All(list.Items.Cast<QueueItem>(), x => Assert.False(x.IsCurrent));
            Assert.NotSame(list.Items[0], list.Items[2]); Assert.Equal(0, creates); Assert.Null(engine.Snapshot.Media);
            Assert.Equal(1.25f, engine.Snapshot.Rate); Assert.Equal(37, engine.Snapshot.Volume); Assert.True(engine.Snapshot.IsMuted);
            Assert.NotNull(window.FindControl<Button>("OpenPlaylistButton")); Assert.NotNull(window.FindControl<Button>("SavePlaylistButton"));
            Assert.True(window.FindControl<CheckBox>("RelativePlaylistPaths")!.IsChecked);
            using var append = await DropData(window, second); DropAt(window, "QueuePanel", append);
            Assert.Equal(4, list.ItemCount); Assert.Equal(PlaybackState.Playing, engine.Snapshot.State); Assert.Equal(1.25f, engine.Snapshot.Rate);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Failed_all_invalid_or_cancelled_import_leaves_current_playback_unchanged_but_empty_clears_it()
    {
        using var f = new PlaylistWindowFiles(); var media = f.Media("one.mp3"); using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFile(media); var before = engine.Snapshot;
            await window.OpenPlaylistAsync(Path.Combine(f.Root, "absent.m3u8")); Assert.Equal(before, engine.Snapshot);
            var bad = Path.Combine(f.Root, "bad.m3u8"); File.WriteAllText(bad, "https://invalid.test/song.mp3\nmissing.mp3");
            await window.OpenPlaylistAsync(bad); Assert.Equal(before, engine.Snapshot); Assert.True(window.FindControl<TextBlock>("ErrorLabel")!.IsVisible);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await window.OpenPlaylistAsync(f.Playlist(media), cancelled.Token); Assert.Equal(before, engine.Snapshot);
            var empty = Path.Combine(f.Root, "empty.m3u8"); File.WriteAllText(empty, "#EXTM3U\n"); await window.OpenPlaylistAsync(empty);
            Assert.Empty(window.FindControl<ListBox>("QueueList")!.Items); Assert.Equal(PlaybackState.Stopped, engine.Snapshot.State);
            Assert.DoesNotContain("one.mp3", window.FindControl<TextBlock>("StatusLabel")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Normal_close_saves_M3u8_and_restart_restores_queue_without_current_selection_or_autoplay()
    {
        using var f = new PlaylistWindowFiles(); var first = f.Media("one.mp3"); var second = f.Media("two.flac"); var sessionPath = Path.Combine(f.Root, "last-queue.m3u8");
        var firstWindow = new MainWindow(new MockPlaybackEngine(), true, sessionPlaylistPath: sessionPath);
        firstWindow.Show(); firstWindow.OpenFiles([first, second, first]); firstWindow.Close(); await WaitForPlaylistClose(firstWindow);
        Assert.Contains(first, File.ReadAllText(sessionPath));
        var creates = 0; using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        var window = new MainWindow(engine, false, engine.InitializeAsync, sessionPlaylistPath: sessionPath);
        try
        {
            window.Show(); await window.RestoreQueueAsync();
            var list = window.FindControl<ListBox>("QueueList")!; Assert.Equal(3, list.ItemCount); Assert.Equal(-1, list.SelectedIndex);
            Assert.All(list.Items.Cast<QueueItem>(), x => Assert.False(x.IsCurrent)); Assert.Equal(0, creates); Assert.Null(engine.Snapshot.Media);
            Click(window, "PlayButton"); await Task.Delay(30); Dispatcher.UIThread.RunJobs(); Assert.Equal(first, engine.Snapshot.Media!.Path);
        }
        finally { window.Close(); await WaitForPlaylistClose(window); }
    }

    [AvaloniaFact]
    public async Task Closing_during_restore_preserves_previous_snapshot_and_ignores_late_completion()
    {
        using var f = new PlaylistWindowFiles(); var path = f.Playlist(f.Media("one.mp3")); var original = File.ReadAllBytes(path);
        var pending = new TaskCompletionSource<PlaylistReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var files = new DelayedPlaylistService(pending.Task); using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, playlistFiles: files, sessionPlaylistPath: path); window.Show();
        var restore = window.RestoreQueueAsync(); window.Close(); pending.SetResult(new([f.Media("two.flac")], 1, 0)); await restore;
        Assert.Equal(original, File.ReadAllBytes(path)); Assert.Throws<ObjectDisposedException>(engine.Play);
    }

    [AvaloniaFact]
    public async Task First_play_from_import_prepares_asynchronously_and_close_prevents_late_play()
    {
        using var f = new PlaylistWindowFiles(); var path = f.Playlist(f.Media("one.mp3"));
        var prepare = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true, () => prepare.Task);
        window.Show(); await window.OpenPlaylistAsync(path); Click(window, "PlayButton");
        Assert.Null(engine.Snapshot.Media); Assert.Contains("Starting", window.FindControl<TextBlock>("StatusLabel")!.Text);
        window.Close(); prepare.SetResult(); await Task.Delay(30); Dispatcher.UIThread.RunJobs();
        Assert.Throws<ObjectDisposedException>(engine.Play);
    }

    [AvaloniaFact]
    public async Task Saving_relative_playlist_preserves_paused_playback_and_round_trips()
    {
        using var f = new PlaylistWindowFiles(); var first = f.Media("one.mp3"); using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFiles([first, first]); Click(window, "PauseButton"); var before = engine.Snapshot;
            var path = Path.Combine(f.Root, "saved.m3u8"); await window.SavePlaylistAsync(path);
            Assert.Equal(before, engine.Snapshot); Assert.DoesNotContain(f.Root, File.ReadAllText(path));
            await window.OpenPlaylistAsync(path); Assert.Equal(2, window.FindControl<ListBox>("QueueList")!.ItemCount);
        }
        finally { window.Close(); }
    }

    private static async Task WaitForPlaylistClose(MainWindow window)
    {
        for (var i = 0; window.IsVisible && i < 300; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task File_disappearing_after_restore_read_does_not_overwrite_previous_snapshot()
    {
        using var f = new PlaylistWindowFiles(); var media = f.Media("vanished.mp3"); var path = f.Playlist(media);
        var original = File.ReadAllBytes(path); File.Delete(media);
        var window = new MainWindow(new MockPlaybackEngine(), true,
            playlistFiles: new ReadResultPlaylistService(new([media], 1, 0)), sessionPlaylistPath: path);
        window.Show(); await window.RestoreQueueAsync(); window.Close(); await WaitForPlaylistClose(window);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    private sealed class ReadResultPlaylistService(PlaylistReadResult result) : IPlaylistFileService
    {
        public Task<PlaylistReadResult> LoadAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task SaveAsync(string path, IReadOnlyList<string> paths, bool relativePaths, CancellationToken cancellationToken = default) =>
            new PlaylistFileService().SaveAsync(path, paths, relativePaths, cancellationToken);
    }

    private sealed class DelayedPlaylistService(Task<PlaylistReadResult> pending) : IPlaylistFileService
    {
        public Task<PlaylistReadResult> LoadAsync(string path, CancellationToken cancellationToken = default) => pending;
        public Task SaveAsync(string path, IReadOnlyList<string> paths, bool relativePaths, CancellationToken cancellationToken = default) => throw new Exception("Pending restore must not overwrite snapshot");
    }
    private sealed class PlaylistWindowFiles : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "lmp-ui-playlist-" + Guid.NewGuid());
        public PlaylistWindowFiles() => Directory.CreateDirectory(Root);
        public string Media(string name) { var path = Path.Combine(Root, name); File.WriteAllText(path, "fixture"); return path; }
        public string Playlist(params string[] files) { var path = Path.Combine(Root, Guid.NewGuid()+".m3u8"); File.WriteAllText(path, "#EXTM3U\n"+string.Join('\n', files)); return path; }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
