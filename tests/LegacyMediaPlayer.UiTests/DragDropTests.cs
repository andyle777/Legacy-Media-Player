using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.UiTests;

public partial class PlayerWindowTests
{
    [AvaloniaFact]
    public async Task Main_drop_replaces_queue_plays_first_valid_file_and_preserves_order_and_duplicates()
    {
        using var files = new DropTestFiles("old.wav", "first.MP3", "ignored.txt", "second.mkv", "missing.flac");
        using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFile(files[0]);
            using var data = await DropData(window, files[1], files[2], files[3], files[1], files[4]);
            File.Delete(files[4]); // It existed when Explorer's transfer was created.
            DropAt(window, "VideoBorder", data);
            Assert.Equal(new[] { files[1], files[3], files[1] }, QueuePaths(window));
            Assert.Equal(files[1], engine.Snapshot.Media!.Path);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            Assert.True(window.FindControl<TextBlock>("ErrorLabel")!.IsVisible);
            Assert.True(File.Exists(files[1])); Assert.True(File.Exists(files[2]));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("QueuePanel", RawInputModifiers.None)]
    [InlineData("VideoBorder", RawInputModifiers.Control)]
    public async Task Append_drop_preserves_paused_track_position_preferences_and_existing_queue(string target, RawInputModifiers modifiers)
    {
        using var files = new DropTestFiles("playing.wav", "next.flac", "video.mp4");
        using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFile(files[0]); engine.Seek(TimeSpan.FromSeconds(34));
            engine.SetVolume(37); engine.SetMuted(true); Click(window, "PauseButton");
            var before = engine.Snapshot;
            using var data = await DropData(window, files[1], files[2]);
            DropAt(window, target, data, modifiers);
            Assert.Equal(new[] { files[0], files[1], files[2] }, QueuePaths(window));
            Assert.Equal(before, engine.Snapshot);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Invalid_or_unsupported_drop_does_not_replace_current_media_or_start_codecs()
    {
        using var files = new DropTestFiles("current.wav", "unsupported.exe");
        var creates = 0;
        using var deferred = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(true, true, true));
        var window = new MainWindow(deferred, false, deferred.InitializeAsync);
        try
        {
            window.Show();
            using var unsupported = await DropData(window, files[1]);
            DropAt(window, "VideoBorder", unsupported);
            using var text = new DataTransfer(); text.Add(DataTransferItem.CreateText(files[0]));
            DropAt(window, "VideoBorder", text);
            Assert.Empty(QueuePaths(window)); Assert.Equal(0, creates);
        }
        finally { window.Close(); }
        using var engine = new MockPlaybackEngine();
        var playing = new MainWindow(engine, true);
        try
        {
            playing.Show(); playing.OpenFile(files[0]); var before = engine.Snapshot;
            using var malformed = new DataTransfer();
            malformed.Add(DataTransferItem.Create<IStorageItem>(DataFormat.File, () => throw new InvalidOperationException("Broken transfer")));
            DropAt(playing, "VideoBorder", malformed);
            Assert.Equal(before, engine.Snapshot); Assert.Equal(new[] { files[0] }, QueuePaths(playing));
            Assert.True(playing.FindControl<TextBlock>("ErrorLabel")!.IsVisible);
        }
        finally { playing.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Shift)]
    public async Task Drag_feedback_advertises_copy_only_and_clears_on_leave_without_starting_playback(RawInputModifiers modifiers)
    {
        using var files = new DropTestFiles("hover.wav");
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); using var data = await DropData(window, files[0]);
            var border = window.FindControl<Border>("VideoBorder")!; var original = border.BorderBrush;
            var effect = DragDropEffects.None;
            window.AddHandler(DragDrop.DragEnterEvent, (_, e) => effect = e.DragEffects, RoutingStrategies.Bubble, true);
            window.DragDrop(DropPoint(window, "VideoBorder"), RawDragEventType.DragEnter, data, DragDropEffects.Copy | DragDropEffects.Move, modifiers);
            Assert.Equal(DragDropEffects.Copy, effect); Assert.NotEqual(original, border.BorderBrush);
            Assert.Null(engine.Snapshot.Media);
            window.DragDrop(DropPoint(window, "VideoBorder"), RawDragEventType.DragLeave, data, DragDropEffects.Copy);
            Assert.Equal(original, border.BorderBrush);
            DropAt(window, "VideoBorder", data, effects: DragDropEffects.Move);
            Assert.Empty(QueuePaths(window)); Assert.True(File.Exists(files[0]));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Drop_copies_transfer_before_async_startup_and_rejects_second_drop_while_busy()
    {
        using var files = new DropTestFiles("first.wav", "second.mp4");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true, () => gate.Task);
        try
        {
            window.Show();
            using (var first = await DropData(window, files[0])) DropAt(window, "VideoBorder", first);
            Assert.False(window.FindControl<Button>("OpenButton")!.IsEnabled);
            using var second = await DropData(window, files[1]); DropAt(window, "VideoBorder", second);
            gate.SetResult(); await Task.Delay(50); Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { files[0] }, QueuePaths(window)); Assert.Equal(files[0], engine.Snapshot.Media!.Path);
        }
        finally { gate.TrySetResult(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Closing_while_drop_waits_for_startup_does_not_resume_disposed_playback()
    {
        using var files = new DropTestFiles("first.wav");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true, () => gate.Task);
        window.Show(); using var data = await DropData(window, files[0]); DropAt(window, "VideoBorder", data);
        Assert.False(window.FindControl<Button>("OpenButton")!.IsEnabled);
        window.Close(); gate.SetResult(); await Task.Delay(50); Dispatcher.UIThread.RunJobs();
        Assert.Throws<ObjectDisposedException>(engine.Play);
    }


    [AvaloniaFact]
    public async Task Queue_drop_on_empty_player_starts_first_item_like_add()
    {
        using var files = new DropTestFiles("first.flac", "second.wav");
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); using var data = await DropData(window, files[0], files[1]);
            DropAt(window, "QueuePanel", data);
            Assert.Equal(new[] { files[0], files[1] }, QueuePaths(window));
            Assert.Equal(files[0], engine.Snapshot.Media!.Path);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Fullscreen_ctrl_drop_appends_and_replacement_audio_exits_after_stream_discovery()
    {
        using var files = new DropTestFiles("first.mp4", "second.mkv", "audio.wav");
        using var engine = new VideoEventsEngine(); var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(files[0]); engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs(); Press(window, PhysicalKey.F11);
            engine.Seek(TimeSpan.FromSeconds(23)); Press(window, PhysicalKey.Space);
            var before = engine.Snapshot;
            using var append = await DropData(window, files[1]);
            DropAt(window, "VideoBorder", append, RawInputModifiers.Control);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Assert.Equal(before, engine.Snapshot);
            Assert.Equal(new[] { files[0], files[1] }, QueuePaths(window));
            using var audio = await DropData(window, files[2]); DropAt(window, "VideoBorder", audio);
            engine.Seek(TimeSpan.FromSeconds(1)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(new[] { files[2] }, QueuePaths(window));
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            Assert.True(window.FindControl<TextBlock>("Placeholder")!.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    private static string[] QueuePaths(MainWindow window) => window.FindControl<ListBox>("QueueList")!.Items.Cast<QueueItem>().Select(i => i.Source.Path).ToArray();
    private static Point DropPoint(MainWindow window, string name)
    {
        var target = window.FindControl<Control>(name)!;
        return target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
    }
    private static void DropAt(MainWindow window, string target, IDataTransfer data, RawInputModifiers modifiers = RawInputModifiers.None, DragDropEffects effects = DragDropEffects.Copy)
    {
        var point = DropPoint(window, target);
        window.DragDrop(point, RawDragEventType.DragEnter, data, effects, modifiers);
        window.DragDrop(point, RawDragEventType.Drop, data, effects, modifiers);
    }
    private static async Task<DataTransfer> DropData(MainWindow window, params string[] paths)
    {
        var data = new DataTransfer();
        foreach (var path in paths)
        {
            var file = await window.StorageProvider.TryGetFileFromPathAsync(path);
            Assert.NotNull(file); data.Add(DataTransferItem.CreateFile(file));
        }
        return data;
    }
    private sealed class DropTestFiles(params string[] names) : IDisposable
    {
        private readonly string _directory = Create(names);
        public string this[int index] => Path.Combine(_directory, names[index]);
        private static string Create(string[] names)
        {
            var directory = Path.Combine(Path.GetTempPath(), "legacy-drop-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            foreach (var name in names) File.WriteAllBytes(Path.Combine(directory, name), [1]);
            return directory;
        }
        public void Dispose() { foreach (var name in names) File.Delete(Path.Combine(_directory, name)); Directory.Delete(_directory); }
    }
}
