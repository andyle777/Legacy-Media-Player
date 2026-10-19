using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
    public void Mode_controls_are_accessible_cycle_without_starting_native_playback_and_fit_minimum_width()
    {
        var creates = 0;
        using var deferred = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(true, true, true));
        var window = new MainWindow(deferred, false, deferred.InitializeAsync) { Width = 900 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var repeat = window.FindControl<Button>("RepeatButton"); var shuffle = window.FindControl<Button>("ShuffleButton");
            Assert.NotNull(repeat); Assert.NotNull(shuffle);
            Assert.Equal("Repeat: Off", repeat.Content);
            foreach (var label in new[] { "Repeat: All", "Repeat: One", "Repeat: Off" })
            {
                Click(window, "RepeatButton"); Dispatcher.UIThread.RunJobs(); Assert.Equal(label, repeat.Content);
                Assert.Contains(label, Avalonia.Automation.AutomationProperties.GetName(repeat));
                var muteButton = window.FindControl<Button>("MuteButton")!;
                Assert.True(muteButton.TranslatePoint(new(0, 0), window)!.Value.X + muteButton.Bounds.Width <= window.ClientSize.Width,
                    $"{label}: Mute right={muteButton.TranslatePoint(new(0, 0), window)!.Value.X + muteButton.Bounds.Width}, client={window.ClientSize.Width}; " +
                    string.Join(", ", new[] { "PreviousButton", "PlayButton", "PauseButton", "StopButton", "NextButton", "RepeatButton", "ShuffleButton", "VolumeSlider", "MuteButton" }.Select(n => n + "=" + window.FindControl<Control>(n)!.Bounds)));
            }
            Click(window, "ShuffleButton"); Assert.Equal("Shuffle: On", shuffle.Content);
            Click(window, "MuteButton"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(0, creates);
            var mute = window.FindControl<Button>("MuteButton")!;
            var point = shuffle.TranslatePoint(new(0, 0), window)!.Value;
            Assert.True(point.X + shuffle.Bounds.Width < window.Width);
            var mutePoint = mute.TranslatePoint(new(0, 0), window)!.Value;
            Assert.True(mutePoint.Y > point.Y || mutePoint.X >= point.X + shuffle.Bounds.Width);
            Assert.True(mute.TranslatePoint(new(0, 0), window)!.Value.X + mute.Bounds.Width <= window.ClientSize.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Playback_modes_restore_with_empty_queue_and_save_with_audio_without_session_paths()
    {
        var path = Path.GetTempFileName(); var store = new AudioPreferenceStore(path);
        store.Save(new(37, true, RepeatMode.One, true));
        using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, preferences: store.Load(), savePlaybackPreferences: store.Save);
        try
        {
            window.Show();
            Assert.Equal("Repeat: One", window.FindControl<Button>("RepeatButton")?.Content);
            Assert.Equal("Shuffle: On", window.FindControl<Button>("ShuffleButton")?.Content);
            Assert.Equal(PlaybackState.Empty, engine.Snapshot.State);
            Assert.Empty(window.FindControl<ListBox>("QueueList")!.Items);
            Click(window, "RepeatButton"); Click(window, "ShuffleButton"); window.Close();
            Assert.Equal(new AudioPreferences(37, true), store.Load());
            Assert.DoesNotContain(path, File.ReadAllText(path));
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task Drop_append_and_replacement_preserve_modes_and_reset_only_replacement_history()
    {
        using var files = new DropTestFiles("first.wav", "second.flac", "video.mp4");
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFile(files[0]);
            Assert.NotNull(window.FindControl<Button>("ShuffleButton"));
            Click(window, "ShuffleButton"); Click(window, "RepeatButton");
            Click(window, "PauseButton"); var paused = engine.Snapshot;
            using var append = await DropData(window, files[1], files[2]);
            DropAt(window, "QueuePanel", append); Assert.Equal(paused, engine.Snapshot);
            Click(window, "NextButton"); Assert.NotEqual(files[0], engine.Snapshot.Media!.Path);
            using var replace = await DropData(window, files[2], files[1]);
            DropAt(window, "VideoBorder", replace);
            Assert.Equal(files[2], engine.Snapshot.Media!.Path); Assert.Equal(2, QueuePaths(window).Length);
            Assert.False(window.FindControl<Button>("PreviousButton")!.IsEnabled);
            Assert.Equal("Repeat: All", window.FindControl<Button>("RepeatButton")!.Content);
            Assert.Equal("Shuffle: On", window.FindControl<Button>("ShuffleButton")!.Content);
            Dispatcher.UIThread.RunJobs();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Fullscreen_and_existing_keyboard_navigation_preserve_modes()
    {
        using var files = new DropTestFiles("first.mp4", "second.mkv");
        using var engine = new VideoEventsEngine(); var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFiles([files[0], files[1]]);
            Assert.NotNull(window.FindControl<Button>("RepeatButton"));
            Click(window, "RepeatButton"); Click(window, "ShuffleButton");
            engine.VideoOutput(true, true); Dispatcher.UIThread.RunJobs(); Press(window, PhysicalKey.F11);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Press(window, PhysicalKey.Escape);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
            Assert.Equal(files[1], engine.Snapshot.Media!.Path);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Control);
            Assert.Equal(files[0], engine.Snapshot.Media!.Path);
            Assert.Equal("Repeat: All", window.FindControl<Button>("RepeatButton")!.Content);
            Assert.Equal("Shuffle: On", window.FindControl<Button>("ShuffleButton")!.Content);
        }
        finally { window.Close(); }
    }
}
