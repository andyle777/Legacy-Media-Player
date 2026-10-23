using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.UiTests;

public partial class PlayerWindowTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancellation_or_close_abandons_blocked_validation_without_late_commit(bool close)
    {
        using var f = new PlaylistWindowFiles(); var old = f.Media("old.mp3"); var next = f.Media("new.flac");
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, playlistFiles: new ReadResultPlaylistService(new([next], 1, 0)),
            sessionPlaylistPath: Path.Combine(f.Root, "last.m3u8"), validatePlaylistFile: path =>
            { entered.TrySetResult(); release.Wait(); try { return MediaSource.FromFile(path); } finally { finished.TrySetResult(); } });
        window.Show(); window.OpenFile(old); var importing = window.OpenPlaylistAsync("input.m3u8", cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            if (close) { window.Close(); await WaitForPlaylistClose(window); } else cancel.Cancel();
            Assert.Same(importing, await Task.WhenAny(importing, Task.Delay(1000)));
            if (close) Assert.Throws<ObjectDisposedException>(engine.Play);
            else Assert.Equal(old, engine.Snapshot.Media!.Path);
            release.Set(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(3)); await importing; Dispatcher.UIThread.RunJobs();
            if (!close) Assert.Equal(old, Assert.IsType<QueueItem>(Assert.Single(window.FindControl<ListBox>("QueueList")!.Items)).Source.Path);
        }
        finally { release.Set(); await importing; if (!close) { window.Close(); await WaitForPlaylistClose(window); } }
    }

    [AvaloniaFact]
    public async Task Validation_failure_preserves_playback_and_drop_is_rejected_only_while_import_is_busy()
    {
        using var f = new PlaylistWindowFiles(); var old = f.Media("old.mp3"); var next = f.Media("new.flac");
        var reader = new ControlledPlaylistReader(); using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, playlistFiles: reader, validatePlaylistFile: _ => throw new UnauthorizedAccessException("Controlled inaccessible media"));
        window.Show(); window.OpenFile(old); var importing = window.OpenPlaylistAsync("input.m3u8");
        try
        {
            await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var drop = await DropData(window, next); DropAt(window, "QueuePanel", drop);
            Assert.Single(window.FindControl<ListBox>("QueueList")!.Items);
            reader.Release.TrySetResult(new([old, next], 2, 0)); await importing;
            Assert.Equal(old, engine.Snapshot.Media!.Path); Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            Assert.Contains("inaccessible", window.FindControl<TextBlock>("ErrorLabel")!.Text);
            using var after = await DropData(window, next); DropAt(window, "QueuePanel", after);
            Assert.Equal(2, window.FindControl<ListBox>("QueueList")!.ItemCount);
        }
        finally { reader.Release.TrySetResult(new([], 0, 0)); await importing; window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(100)] [InlineData(1000)] [InlineData(10000)]
    public async Task Large_playlist_validation_is_off_dispatcher_and_commit_keeps_order_duplicates_and_modes(int count)
    {
        using var f = new PlaylistWindowFiles(); var old = f.Media("old.mp3"); var unicode = f.Media("âm nhạc Việt.mp3"); var second = f.Media("two.flac");
        var paths = Enumerable.Range(0, count).Select(i => i % 3 == 1 ? second : unicode).ToArray();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); var onUi = false; var probes = 0;
        using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, preferences: new(33, true, RepeatMode.All, true, 1.5f),
            playlistFiles: new ReadResultPlaylistService(new(paths, count, 0)), validatePlaylistFile: path =>
            {
                if (Dispatcher.UIThread.CheckAccess()) { onUi = true; return MediaSource.FromFile(path); }
                entered.Set(); release.Wait(); Interlocked.Increment(ref probes); return MediaSource.FromFile(path);
            });
        window.Show(); window.OpenFile(old); var importing = window.OpenPlaylistAsync("large.m3u8");
        try
        {
            await Task.WhenAny(importing, Task.Run(() => entered.Wait(TimeSpan.FromSeconds(3))));
            Assert.False(onUi); Assert.True(entered.IsSet); Assert.False(importing.IsCompleted);
            var responsive = false; await Dispatcher.UIThread.InvokeAsync(() => responsive = true); Assert.True(responsive);
            Assert.Equal(old, engine.Snapshot.Media!.Path);
            release.Set(); await importing;
            var list = window.FindControl<ListBox>("QueueList")!;
            Assert.Equal(paths, list.Items.Cast<QueueItem>().Select(x => x.Source.Path));
            Assert.NotSame(list.Items[0], list.Items[2]); Assert.Equal(-1, list.SelectedIndex);
            Assert.All(list.Items.Cast<QueueItem>(), x => Assert.False(x.IsCurrent));
            Assert.Equal(count, probes); Assert.Equal(1.5f, engine.Snapshot.Rate); Assert.True(engine.Snapshot.IsMuted);
            Assert.Contains("All", window.FindControl<Button>("RepeatButton")!.Content!.ToString());
            Assert.Contains("On", window.FindControl<Button>("ShuffleButton")!.Content!.ToString());
        }
        finally { release.Set(); await importing; window.Close(); }
    }

    [AvaloniaFact]
    public async Task Superseded_import_cannot_overwrite_a_newer_import_and_file_work_is_serialized()
    {
        using var f = new PlaylistWindowFiles(); var old = f.Media("old.mp3"); var first = f.Media("first.flac"); var newest = f.Media("Việt.mp3");
        var service = new ControlledPlaylistReader(); using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, playlistFiles: service);
        window.Show(); window.OpenFile(old);
        var older = window.OpenPlaylistAsync(first); await service.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var newer = window.OpenPlaylistAsync(newest);
        try
        {
            // An uncooperative reader remains blocked, but its caller must be abandoned.
            Assert.Same(older, await Task.WhenAny(older, Task.Delay(1000)));
            Assert.Equal(old, engine.Snapshot.Media!.Path);
            Assert.Equal(1, service.Calls);
            service.Release.TrySetResult(new([first], 1, 0));
            await Task.WhenAll(older, newer);
            var list = window.FindControl<ListBox>("QueueList")!;
            Assert.Equal(newest, Assert.IsType<QueueItem>(Assert.Single(list.Items)).Source.Path);
            Assert.Equal(2, service.Calls); Assert.All(list.Items.Cast<QueueItem>(), x => Assert.False(x.IsCurrent));
        }
        finally { service.Release.TrySetResult(new([first], 1, 0)); await Task.WhenAll(older, newer); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Cancelling_an_uncooperative_import_returns_without_partial_replacement_or_late_mutation()
    {
        using var f = new PlaylistWindowFiles(); var old = f.Media("old.mp3"); var next = f.Media("next.flac");
        var service = new ControlledPlaylistReader(); using var engine = new MockPlaybackEngine(); using var cancel = new CancellationTokenSource();
        var window = new MainWindow(engine, true, playlistFiles: service); window.Show(); window.OpenFile(old);
        var importing = window.OpenPlaylistAsync(next, cancel.Token); await service.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancel.Cancel();
        try
        {
            Assert.Same(importing, await Task.WhenAny(importing, Task.Delay(1000)));
            Assert.Equal(old, engine.Snapshot.Media!.Path);
            service.Release.TrySetResult(new([next], 1, 0)); await importing; Dispatcher.UIThread.RunJobs();
            Assert.Equal(old, Assert.IsType<QueueItem>(Assert.Single(window.FindControl<ListBox>("QueueList")!.Items)).Source.Path);
        }
        finally { service.Release.TrySetResult(new([next], 1, 0)); await importing; window.Close(); }
    }

    [AvaloniaFact]
    public async Task Closing_abandons_an_uncooperative_startup_restore_without_waiting_for_filesystem_work()
    {
        using var f = new PlaylistWindowFiles(); var path = f.Playlist(f.Media("one.mp3")); var original = File.ReadAllBytes(path);
        var service = new ControlledPlaylistReader(); var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, playlistFiles: service, sessionPlaylistPath: path); window.Show();
        var importing = window.RestoreQueueAsync(); await service.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); window.Close();
        try
        {
            Assert.False(window.IsVisible);
            Assert.Same(importing, await Task.WhenAny(importing, Task.Delay(1000)));
            Assert.Equal(original, File.ReadAllBytes(path)); Assert.Throws<ObjectDisposedException>(engine.Play);
        }
        finally { service.Release.TrySetResult(new([], 0, 0)); await importing; }
    }

    private sealed class ControlledPlaylistReader : IPlaylistFileService
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<PlaylistReadResult> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public Task<PlaylistReadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
        { if (Interlocked.Increment(ref Calls) == 1) { Entered.TrySetResult(); return Release.Task; } return Task.FromResult(new PlaylistReadResult([path], 1, 0)); }
        public Task SaveAsync(string path, IReadOnlyList<string> paths, bool relativePaths, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No save expected");
    }
}
